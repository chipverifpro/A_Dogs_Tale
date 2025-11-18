using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles scent diffusion/decay for a 3D cell graph using heightfield neighbors.
/// Uses two layers: airborne (air) and ground (surface).
/// </summary>
public class ScentAirGround : MonoBehaviour
{
    [Header("External References")]
    public ObjectDirectory dir;
    public DungeonGenerator gen;

    [Tooltip("All cells that participate in scent simulation.")]
    public List<Cell> scentCells = new List<Cell>(); // Cache a list including only cells with any scent
    
    public int currentAgentId = -1;      // if this changes, redraw all scents
    public int previousAgentIdVisualized = -1;  // used to tell if currentAgentId changed

    [Header("Air Layer Settings")]
    public bool airScentVisible = true;
    private bool airScentWasVisible = false; // memory, if changed, then update all
    [Tooltip("Global multiplier to all external sources for air depositing (usually 1.0).")]
    public float airScentDepositRate = 1.0f;
    [Range(0f, 1f)]
    public float airDiffusionRate = 0.15f;  // how strongly air mixes between neighbors per tick
    [Range(0f, 1f)]
    public float airDecayRate = 0.2f;       // per-second fraction of airborne scent lost

    [Header("Ground Layer Settings")]
    public bool groundScentVisible = true;
    private bool groundScentWasVisible = false;
    [Tooltip("Global multiplier to all external sources for ground depositing (usually 1.0).")]
    public float groundScentDepositRate = 1.0f; // global multiplier to all extenal sources
    [Range(0f, 1f)]
    public float groundDiffusionRate = 0.05f;  // slower spread across surfaces

    [Range(0f, 1f)]
    public float groundDecayRate = 0.01f;      // per-second ground scent loss (longer lived)

    [Header("Phase Coupling (Air <-> Ground)")]
    [Tooltip("Per-second fraction of airborne scent that deposits to ground.")]
    [Range(0f, 1f)]
    public float airToGroundRate = 0.10f;

    [Tooltip("Per-second fraction of ground scent that re-emits into air.")]
    [Range(0f, 1f)]
    public float groundToAirRate = 0.03f;

    [Header("Simulation Settings")]
    public float simulationTimeStep = 0.1f; // seconds per step
    public bool runOnStart = true;
    public float practically_zero = 0.00001f;  // if anything is this tiny, ignore or make it go away

    [Header("Visualization")]
    public ElementStore elementStore;   // ScriptableObject you already use

    // Minimum change before we bother updating visuals.
    // 1 / 256 ~ alpha resolution in 8-bit channel.
    public float scentVisualThreshold = 1f / 256f;

    // Intensity that maps to alpha = 1 (clamp everything above this).
    public float maxVisualIntensity = 1f;

    // Base colors for air & ground scent (alpha will be overridden).
    public Color airBaseColor = new Color(0.2f, 1f, 1f, 0f);
    public Color groundBaseColor = new Color(0.5f, 1f, 0.2f, 0f);

    // used repeatedly in ScentPhysicsStepOnce():
    private Cell[] neighbors = new Cell[4]; // cache the 4 neighbor cells once per cell examined.  reuse on each scent.  Change to 8 if you want diagonals.
    
    private bool anyScentAirChanged = false;    // tracks whether the GO visuals need updating
    private bool anyScentGroundChanged = false;
    private bool anyScentAirCreated = false;
    private bool anyScentGroundCreated = false;
    private bool scentCamActive = false; // tracks whether scent camera should be on or off


    // REPLACED BY DirFlags:
    // Neighbor offsets in heightfield space (4-connected, HF X/Y; Z stays same)
    // Adjust/add vertical neighbors if desired.
    private static readonly Vector2Int[] neighborOffsets =  // TODO: Use DirFlags and  DirFlagsEx.ToVector2Int(dirFlags);
    {
        new Vector2Int( 1,  0), // N
        new Vector2Int( 0,  1), // E
        new Vector2Int(-1,  0), // S
        new Vector2Int( 0, -1), // W
    };

    private Coroutine _simulationCoroutine;     // keep pointer in order to stop.

    /*
        USAGE call...

        dir.scents.AddScentToCell(
            player_cell,
            agentId: 1,
            airAmount: 1f,
            groundAmount: 0.1f);

        Situation	            airAmount	groundAmount	Description
        Default (normal gameplay)	1.0f	0.1f	General player emission.
        Running or excited	        1.5f	0.2f	More scent churn; stronger trail.
        Wet dog / muddy ground	    1.0f	0.3f	Surface absorbs more scent.
        Windy outdoors	            2.0f	0.05f	Air plume stronger, ground weaker.
        Indoors (stale air)	        0.7f	0.15f	Less air diffusion, more buildup.
        Object or relic	            0.0f	0.2f	Stationary odor source on the
        Small animal – squirrel	    0.6 f	0.05 f	Light air scent, faint ground residue; up in trees emits mainly airborne scent pockets.
        Bird (perched or flying)	0.4 f	0.00 f	Air-only signature that drifts quickly and fades fast.
        Rabbit (hopping)	        0.8 f	0.12 f	Bursty ground contacts where feet hit; intermittent airborne whiffs.
        Dog – small (Chihuahua, Corgi)	0.8 f	0.08 f	Less body mass → lighter scent field; trail still trackable.
        Dog – medium (Shepherd, Retriever)1.0 f	0.10 f	Baseline emitter for average canine size.
        Dog – large (Mastiff, Wolf)	1.4 f	0.15 f	More surface area → stronger scent plume and trail.
        Human (standing / walking)	1.2 f	0.05 f	Warm, vertical air column; minimal ground deposit from shoes.
        Hot dog – grilling	        2.0 f	0.00 f	Intense airborne food scent; convection carries far.
        Hot dog – on picnic table	1.0 f	0.10 f	Still aromatic but mostly localized; slight scent absorbed by surface.
        Machine – Roomba (indoor)	0.3 f	0.20 f	Small motor odor + consistent ground residue from wheels.
        Machine – lawn mower (outdoor)	1.5 f	0.30 f	Hot e
    
    Usage Notes
	•	Scaling: Treat these as multipliers — for example, a wet dog indoors could use
            airAmount = 1.0 × indoor_factor × wet_factor → ~0.8 f air, 0.25 f ground.
	•	Air vs. Ground ratio intuition:
	•	    Air > Ground → “smellable from afar.”
	•	    Ground ≥ Air → “trackable trail.”

    */



    private void Start()
    {
        if (runOnStart)
        {
            StartScentSimulation();
        }
    }

    // monitor for changes to who or what is visualized, then clear/update/redraw
    // (this allows developer to change settings on-the-fly and the visuals track that)
    // Normally the redraw could be specified at the place the change is made.
    private void Update_old()
    {
        if ((groundScentVisible != groundScentWasVisible) 
            || (airScentVisible != airScentWasVisible)
            || (currentAgentId != previousAgentIdVisualized))
        {
            // some visiblity changed...

            // First, check if we need to enable/disable the scent camera
            scentCamActive = (groundScentVisible || airScentVisible) && (currentAgentId!=-1);
            if (scentCamActive != dir.scentCam.enabled)
                dir.scentCam.enabled = scentCamActive; // turn scent camera on/off based on possibly changed need
            if (!scentCamActive)
            {
                // update the previous state trackers so we don't turn things back on that shouldn't
                previousAgentIdVisualized = currentAgentId;
                airScentWasVisible        = airScentVisible;
                groundScentWasVisible     = groundScentVisible;

                return; // scent camera is off, no need to update anything else
            }
            // Scent camera is needed, so update everything
            ClearAllScentVisuals();     // clear anything previously visualized

            ScentSource source = dir.scentRegistry.GetScentSource(currentAgentId);
            ActivateOverlayForSource(source);   // switch agent source
            
            VisualizeCurrentScents();
            ApplyScentUpdates(); // push to GPU immediately
        }
    }
    private void Update()
    {
        bool visibilityOrAgentChanged =
            (groundScentVisible != groundScentWasVisible) ||
            (airScentVisible    != airScentWasVisible)    ||
            (currentAgentId     != previousAgentIdVisualized);

        if (!visibilityOrAgentChanged)
            return;

        // What we *want* now:
        bool wantCamActive = (currentAgentId != -1) && (groundScentVisible || airScentVisible);

        // --- Case 1: we no longer want the scent camera active ---
        if (!wantCamActive)
        {
            // If it was previously active, make sure visuals are cleared once
            if (scentCamActive)
            {
                ClearAllScentVisuals();
                ApplyScentUpdates(); // push hide to GPU, so even if another camera sees fog, it's gone
            }

            scentCamActive = false;
            if (dir.scentCam != null)
                dir.scentCam.enabled = false;

            // Sync "previous" state so this change doesn't keep firing every frame
            previousAgentIdVisualized = currentAgentId;
            airScentWasVisible        = airScentVisible;
            groundScentWasVisible     = groundScentVisible;

            return;
        }

        // --- Case 2: we want the scent camera ON ---
        scentCamActive = true;
        if (dir.scentCam != null && !dir.scentCam.enabled)
            dir.scentCam.enabled = true;

        // Fully reset visuals for a clean overlay
        ClearAllScentVisuals();

        // Switch the overlay to the new source for currentAgentId
        ScentSource source = dir.scentRegistry.GetScentSource(currentAgentId);
        ActivateOverlayForSource(source);

        // Draw the current state for this agent with current visibility flags
        VisualizeCurrentScents();
        ApplyScentUpdates();

        // Sync "previous" state
        previousAgentIdVisualized = currentAgentId;
        airScentWasVisible        = airScentVisible;
        groundScentWasVisible     = groundScentVisible;
    }

    public void StartScentSimulation()
    {
        if (_simulationCoroutine != null)
        {
            StopCoroutine(_simulationCoroutine);    // kill previously still running copy?
        }

        currentAgentId = 1;
        // Call this every time we change map structure (load/build complete): likely to start empty or nearly so before scents start appearing.
        ScentCellsListCreate();

        // start the simulation loop, but keep the Coroutine ID for future StopScentSimulation() calls
        _simulationCoroutine = StartCoroutine(ScentDecayAndSpread());
    }

    public void StopScentSimulation()
    {
        if (_simulationCoroutine != null)
        {
            StopCoroutine(_simulationCoroutine);
            _simulationCoroutine = null;
        }
    }

    // This list is of all cells that have any scent in them.  Used to speed up scent physics.
    // Will likely start at nothing unless physics had been running earlier, like a middle of level load.
    public void ScentCellsListCreate()
    {
        scentCells = new();
        foreach (Room room in dir.gen.rooms)
        {
            foreach (Cell cell in room.cells)
            {
                if ((cell != null) && (cell.scents != null) && (cell.scents.Count != 0))
                    scentCells.Add(cell);
            }
        }
    }

    /// <summary>
    /// Main coroutine that steps the scent field over time using heightfield neighbors.
    /// </summary>
    private IEnumerator ScentDecayAndSpread()
    {
        int original_cell_count = 0;

        if (dir == null || dir.gen == null)
        {
            Debug.LogError("ScentSystem: dir or dir.gen reference is missing.");
            yield break;
        }

        // Wait for build complete.
        if (dir.gen.buildComplete == false)
            yield return new WaitUntil(() => dir.gen.buildComplete == true);    // wait until build is done

        // Ensure the heightfield is ready
        if (!dir.gen.hf_valid || dir.gen.hf == null || dir.gen.hf.Width == 0 || dir.gen.hf.Height == 0)
        {
            Debug.Log("ScentDecayAndSpread: Heightfield not valid, preparing heightfield.");
            dir.gen.PrepareHeightfield();
            yield return null;
        }

        // Call this every time we change map structure (load/build complete): likely to start empty or nearly so before scents start appearing.
        ScentCellsListCreate();
        
        // define the wait time per step funtion for repeated use below. (yield return wait;)
        WaitForSeconds wait = new WaitForSeconds(simulationTimeStep);
        
        //////////////////////////////////////////////////////////
        /// Main physics/visualize loop is one of these two... ///
        //////////////////////////////////////////////////////////
        // recommend using consistancy=true unless performance is a problem.
        if (dir.cfg.scentPhysicsConsistancey == false) // option A is to scale the call by actual time step
        {
            while (true)
            {
                original_cell_count = scentCells.Count;
                var limitedDeltaTime = Mathf.Clamp(Time.deltaTime, 0f, simulationTimeStep * 5f); // prevent huge steps if frame rate drops too low
                ScentPhysicsStepOnce(limitedDeltaTime);

                if (scentCamActive) // only update visuals if scent camera is on
                {
                    VisualizeCurrentScents(original_cell_count);
                    ApplyScentUpdates(); // push to GPU
                }
                yield return wait;
            }
        }
        else // Option B is to use multiple fixed time steps until we catch up.
        {
            float accumulator = 0f;
            float step = simulationTimeStep;
            while (true)
            {
                accumulator += Time.deltaTime;
                accumulator = Mathf.Min(accumulator, step * 5f); // prevents spiral of death if frame rate drops too low, or huge lag spike if game paused.

                if (accumulator >= step)
                {
                    // Must run run one or more steps to catch up
                    while (accumulator >= step)
                    {
                        original_cell_count = scentCells.Count;
                        ScentPhysicsStepOnce(step);
                        accumulator -= step;
                        // might be nice to yield a frame here if several steps are needed? (only if this is a significantly slow function)
                    }

                    if (scentCamActive)     // only update visuals if scent camera is on
                    {
                        VisualizeCurrentScents(original_cell_count);  // run only once after all physics steps complete
                        ApplyScentUpdates(); // push to GPU
                    }
                }
                yield return wait;
            }
        }
    }

    /// <summary>
    /// One simulation step: diffusion, coupling, decay, and commit.
    /// </summary>
    private void ScentPhysicsStepOnce(float dt)
    {
        if (scentCells == null || scentCells.Count == 0)
        {
            scentCells = new(); // create it empty.
            return;
        }
        //Debug.Log($"StepOnce: scentCells has {scentCells.Count} entries.");

        float airDecayFactor = Mathf.Clamp01(1f - airDecayRate * dt);
        float groundDecayFactor = Mathf.Clamp01(1f - groundDecayRate * dt);
        float depositFactor = airToGroundRate * dt;
        float emitFactor = groundToAirRate * dt;

        int cIdx;   // loop index: cell
        int sIdx;   // loop index: scent
        int nIdx;   // loop index: neighbor cell
        int n_sIdx; // loop index: neighbor cell's scent

        int agentId;
        int neighborCount;
        int original_cell_count = scentCells.Count;

        // FIRST PASS: compute deltas and accumulate into airNextDelta / groundNextDelta

        // This loop doesnt include any extra cells we add this pass.
        for (cIdx = 0; cIdx < original_cell_count; cIdx++)
        {
            Cell cell = scentCells[cIdx];
            if (cell == null) continue; // should not happen

            // quick check to see if ANY scents in this cell are non-zero
            bool i_have_scents = false;
            if (cell.scents == null) cell.scents = new();
            foreach (ScentClass scent in cell.scents)
            {
                if (scent.airIntensity >= practically_zero || scent.groundIntensity >= practically_zero)
                {
                    i_have_scents = true;
                    break;
                }
            }

            // cache the cells for all this cell's neighbors for use in each agentId scent entry
            neighborCount = 0;  // count them for later averaging or skipping later loop
            for (nIdx = 0; nIdx < 4; nIdx++)                    // loop 8 times if you want diagonals
            {
                DirFlags dirF = DirFlagsEx.AllCardinals[nIdx];  // use All8[nIdx] if you want diagonals
                DirFlags dDoor = (cell.doors & dirF);
                DirFlags dWall = (cell.walls & dirF);
                if (dWall==DirFlags.None && dDoor==DirFlags.None) // TODO: add open doors
                {
                    neighbors[nIdx] = dir.gen.GetCellFromHf(cell.pos3d.x + DirFlagsEx.ToVector2Int(dirF).x,
                                                            cell.pos3d.y + DirFlagsEx.ToVector2Int(dirF).y,
                                                            cell.pos3d.z,
                                                            threshold: 50); // returns null if no neighbor. that's ok.
                }
                if (neighbors[nIdx] != null) neighborCount++;
                // if we are going to propogate scents to this cell soon, it better be on the scentCells list.
                if (i_have_scents && (neighbors[nIdx] != null) && !scentCells.Contains(neighbors[nIdx])) // WARNING: Contains() is O(N) search, revisit as hashset if many scents coexist.
                    scentCells.Add(neighbors[nIdx]);           // add neighbor cell to list
            }

            // for each different scent at this location
            for (sIdx = 0; sIdx < cell.scents.Count; sIdx++)
            {
                ScentClass scent = cell.scents[sIdx];
                if (scent == null) continue;    // should not happen unless we nulled out a scent (which we don't do)

                agentId = scent.agentId;

                // grab the values at the beginning of the cycle, then initialize the new values to match.
                float baseAir = scent.airIntensity;
                float baseGround = scent.groundIntensity;

                float airValue = baseAir;
                float groundValue = baseGround;

                // if this is true, we will add the agentId to neighbor cells if not there already
                bool thisCellHasThisScent = (baseAir > practically_zero) || (baseGround > practically_zero);

                // calculate neighbor averages.  We already know neighborCount.
                float airNeighborSum = 0f;
                float groundNeighborSum = 0f;

                if (neighborCount > 0) // skip if no neighbors
                {
                    for (nIdx = 0; nIdx < 4; nIdx++)   // for each of the 4 neighbors we already looked up at the beginning of the cell loop
                    {
                        if (neighbors[nIdx] == null) continue; // no neighbor in this direction

                        // Look in the neighbor cell's scents for a matching agentId, index returned as n_sIdx
                        // Add it (createIfNeeded: true) if not found and we have non-zero scent here.
                        n_sIdx = FindAgentIdScentIndex(neighbors[nIdx], agentId, createIfNeeded: false);

                        if (n_sIdx >= 0) // neighbor has a scent with this agentId already, accumulate it
                        {
                            var neighborScent = neighbors[nIdx].scents[n_sIdx];
                            airNeighborSum += neighborScent.airIntensity;
                            groundNeighborSum += neighborScent.groundIntensity;
                        }
                        else // neighbor doesn't have this agentId but we do, add it so it may grow NEXT StepOnce
                        {
                            if (thisCellHasThisScent) // only add agentId if nonzero scent here.
                            {
                                if (neighbors[nIdx].scents == null) neighbors[nIdx].scents = new(); // create list if needed
                                // add this scent agentId to neighbor, with zero intensities
                                ScentClass newScent = new()
                                {
                                    agentId = agentId,
                                    airGOindex = -1,
                                    groundGOindex = -1
                                };
                                neighbors[nIdx].scents.Add(newScent);  // add zero scent to neighbor cell so it can pull in scent next time
                            }
                        }
                    }

                    // Diffusion moves towards neighbor average
                    float airNeighborAverage = airNeighborSum / neighborCount;
                    float groundNeighborAverage = groundNeighborSum / neighborCount;

                    airValue += airDiffusionRate * (airNeighborAverage - baseAir);
                    groundValue += groundDiffusionRate * (groundNeighborAverage - baseGround);
                }

                // Phase coupling: air <-> ground
                float deposit = baseAir * depositFactor; // air -> ground (deposit)
                float emit = baseGround * emitFactor;    // ground -> air (emit)

                airValue = (airValue - deposit) * airDecayFactor + emit;
                groundValue = (groundValue + deposit) * groundDecayFactor - emit;

                // eliminate negative values, or tiny positive ones also
                if (airValue < practically_zero) airValue = 0f;
                if (groundValue < practically_zero) groundValue = 0f;

                // Convert to delta relative to current intensity
                float airDelta = airValue - baseAir;
                float groundDelta = groundValue - baseGround;

                // accumulate this delta to any existing delta left earlier
                scent.airNextDelta += airDelta;
                scent.groundNextDelta += groundDelta;
            } // end for sIdx
        } // end foreach cell

        // SECOND PASS: comit of nextDelta -> intensity
        
        for (cIdx = 0; cIdx < original_cell_count; cIdx++)  // still doesnt include recent additions to list
        {
            Cell cell = scentCells[cIdx];
            if (cell == null) continue;

            // for each different scent at this location
            for (sIdx = 0; sIdx < cell.scents.Count; sIdx++)
            {
                ScentClass scent = cell.scents[sIdx];
                if (scent == null) continue;

                // propogate changes we calculated in first pass above
                //float airDelta    = scent.airNextDelta;
                //float groundDelta = scent.groundNextDelta;

                // only limit VisualThreshold for the visualualized currentAgentId
 
                bool airChanged = scent.airNextDelta != 0;
                bool groundChanged = scent.groundNextDelta != 0;

                // ----- Apply deltas to intensities -----
                // ----- Zero it out if tiny intensity and heading in a negative direction.                if (airChanged)
                if (airChanged)
                {
                    scent.airIntensity += scent.airNextDelta;
                    if ((scent.airIntensity < practically_zero) && scent.airNextDelta <= 0f) scent.airIntensity = 0f;
                    scent.airNextDelta = 0f;
                }

                if (groundChanged)
                {
                    scent.groundIntensity += scent.groundNextDelta;
                    if ((scent.groundIntensity < practically_zero) && scent.groundNextDelta <= 0f) scent.groundIntensity = 0f;
                    scent.groundNextDelta = 0f;
                }
            }
        }
    }

    public void VisualizeCurrentScents(int original_cell_count = -1)
    {
        int cIdx;
        int sIdx;
        bool airChanged;
        bool groundChanged;     
        ScentClass scent;
        Cell cell;

        // Reset needed? We already reset these after changes applied.
        //               Also reset at initial creation.
        //               Could we have changed something elsewhere we shouldn't forget to apply?
        //anyScentAirChanged = false;
        //anyScentGroundChanged = false;
        //anyScentAirCreated = false;
        //anyScentGroundCreated = false;

        // If we have the cell count before the previous physics step, we don't need to look at the newly created entries.
        // Or, we can just look at everything.  The last entries should all nave no scents and loop quickly.
        if (original_cell_count == -1) original_cell_count=scentCells.Count;

        for (cIdx = 0; cIdx < original_cell_count; cIdx++)  // still doesnt include recent additions to list
        {
            cell = scentCells[cIdx];
            if (cell == null) continue;

            if (cell.scents==null || cell.scents.Count==0) continue;    // no scents exists for this cell

            sIdx = FindAgentIdScentIndex(cell, currentAgentId, createIfNeeded: false);
            if (sIdx>=0)
            {
                // scent matches currentAgentId
                scent = cell.scents[sIdx];
                // check for visible and significant intensity changes:
                //          scent is visible AND (change since last update large enough
                //                                OR changed to exactly 0)
                airChanged = airScentVisible && (
                            (Mathf.Abs(scent.airIntensity - scent.airLastVisualized) > scentVisualThreshold)
                            || ((scent.airIntensity==0f) && (scent.airLastVisualized!=0f)));
                groundChanged = groundScentVisible && (
                            (Mathf.Abs(scent.groundIntensity - scent.groundLastVisualized) > scentVisualThreshold)
                            || ((scent.groundIntensity==0f) && (scent.groundLastVisualized!=0f)));
                // update the cell
                ScentVisualizationAtCell(cell, scent, airChanged, groundChanged);
                anyScentAirChanged |= airChanged;
                anyScentGroundChanged |= groundChanged;
                continue; // continue to next cell
            }
            else
            {
                // no scent matching currentAgentId in this cell.  Assume we have alreeady cleared any old one.
                continue;
            }
        }
        // update the previousAgentIdVisualized
        previousAgentIdVisualized = currentAgentId;
        airScentWasVisible = airScentVisible;
        groundScentWasVisible = groundScentVisible;

        //Done.  Don't forget to push all changes with ApplyScentUpdates()!
    }

    private void ScentVisualizationAtCell(Cell cell, ScentClass scent, bool airChanged, bool groundChanged)
    {
        Color color;
        float normalized;

        if (elementStore == null) return;
        //Debug.Log($"Visualize scent at cell {cell.pos}.  Air = {scent.airIntensity}, Ground = {scent.groundIntensity}");

        // ----- Create or update visuals -----


        // =====Air layer

        if (airChanged)        // we should update the air
        {
            if (scent.airIntensity > 0f)
            {
                normalized = Mathf.Clamp01(scent.airIntensity / maxVisualIntensity);
                color = airBaseColor;
                color.a = normalized;

                if (scent.airGOindex < 0) // ===> intensity>0 and no GO exists
                {
                    // First time: create the ScentAir element and store its index
                    scent.airGOindex = elementStore.AddScentAir(cell, color);
                    anyScentAirCreated = true;
                    if (scent.airGOindex < 0) // creation of GOindex failed
                        Debug.LogError($"ScentVisualization: AddScentAir(@{cell.pos}, alpha={color.a}) returned -1");
                }
                else // ===> intensity>0 and GO already exists
                {
                    // Update color of existing instance
                    elementStore.ChangeColor(ElementLayerKind.ScentAir, scent.airGOindex, cell, color);
                }
            }
            else if (scent.airGOindex >= 0)     // ===> intensity==0 and GO exists
            {
                // Just make it fully transparent via ChangeColor.  We could disable it also.
                color = airBaseColor;
                color.a = 0f;
                elementStore.ChangeColor(ElementLayerKind.ScentAir, scent.airGOindex, cell, color);
            }
            // update previously seen value...
            scent.airLastVisualized = scent.airIntensity;
        }

        // =======Ground layer

        if (groundChanged)              // we should update the ground
        {
            if (scent.groundIntensity > 0f)
            {
                normalized = Mathf.Clamp01(scent.groundIntensity / maxVisualIntensity);
                color = groundBaseColor;
                color.a = normalized;

                if (scent.groundGOindex < 0) // ===> intensity>0 and no GO exists
                {
                    // First time: create the ScenGround element and store its index
                    scent.groundGOindex = elementStore.AddScentGround(cell, color);
                    anyScentGroundCreated = true;
                    if (scent.groundGOindex < 0)
                        Debug.LogError($"ScentVisualization: AddScentGround(@{cell.pos}, alpha={color.a}) returned -1");

                }
                else // ===> intensity>0 and GO already exists
                {
                    // Update color of existing instance
                    elementStore.ChangeColor(ElementLayerKind.ScentGround, scent.groundGOindex, cell, color);
                }
            }
            else if (scent.groundGOindex >= 0)     // ===> intensity==0 and GO exists
            {
                // Just make it fully transparent via ChangeColor.  We could disable it also.
                color = groundBaseColor;
                color.a = 0f;
                elementStore.ChangeColor(ElementLayerKind.ScentGround, scent.groundGOindex, cell, color);
            }
            // update previously seen value...
            scent.groundLastVisualized = scent.groundIntensity;
        }
    }

    public void ApplyScentUpdates()
    {
        // If we created any new fog instances, manufacture GOs for them
        if (anyScentAirCreated)
        {
            dir.manufactureGO.BuildNewInstancesForLayer(ElementLayerKind.ScentAir);
        }
        if (anyScentGroundCreated)
        {
            dir.manufactureGO.BuildNewInstancesForLayer(ElementLayerKind.ScentGround);
        }

        // Then apply all color changes (including newly created ones).
        if (anyScentAirChanged || anyScentGroundChanged || anyScentAirCreated || anyScentGroundCreated)
        {
            dir.manufactureGO.ApplyPendingUpdates();    // applies all pending changes to all layers
        }

        //Debug.Log($"ApplyScentUpdates completed. anyScentAir/GroundCreated={anyScentAirCreated}/{anyScentGroundCreated}, anyScentAir/GroundChanged={anyScentAirChanged}/{anyScentGroundChanged}");
        
        // We have applied all changes, reset flags
        anyScentAirCreated = false;
        anyScentGroundCreated = false;
        anyScentAirChanged = false;
        anyScentGroundChanged = false;
    }

    #region Public API

    // Use visualizeImmediately=true to see the effect right away (for player deposit, etc), not as part of the mass update.
    public void DepositScentToCell(Cell cell, ScentSource scentSource, bool visualizeImmediately=false)
    {
        AddScentToCell(cell, scentSource.agentId, scentSource.airDepositRate, scentSource.groundDepositRate);
        
        if (scentCamActive && visualizeImmediately && (scentSource.agentId == currentAgentId))
        {
            int sIdx = FindAgentIdScentIndex(cell, scentSource.agentId, createIfNeeded: false);
            if (sIdx >=0)
            {
                ScentClass scent = cell.scents[sIdx];

                // do a mini propogation of delta to intensity to be visualized immediately
                cell.scents[sIdx].airIntensity += cell.scents[sIdx].airNextDelta;
                cell.scents[sIdx].airNextDelta = 0;
                cell.scents[sIdx].groundIntensity += cell.scents[sIdx].groundNextDelta;
                cell.scents[sIdx].groundNextDelta = 0;

                ScentVisualizationAtCell(cell, scent, airChanged: airScentVisible, groundChanged: groundScentVisible);
                //Debug.LogError($"DepositScentToCell: sIdx = {sIdx} visualized");
                ApplyScentUpdates();    // push it to the GPU

            }
            else
            {
                //Debug.LogError($"DepositScentToCell: sIdx = {sIdx}");
            }
        }
    }

    /// <summary>
    /// Add scent to a specific cell (e.g., from an emitter).
    /// </summary>
    private void AddScentToCell(     // you probably want to use DepositScentToCell() above instead!
        Cell cell,
        int agentId,
        float airAmount,
        float groundAmount)
    {
        int sIdx = FindAgentIdScentIndex(cell, agentId, createIfNeeded: true);
        //Debug.Log($"===>({cell.pos}) sIdx = {sIdx} for agentId = {agentId}");
        //Debug.Log($"Adding scent agentId={agentId} to cell at {cell.pos}. sIdx={sIdx}");
        if (sIdx < 0)
        {
            Debug.Log($"({cell.pos}) sIdx = {sIdx} for agentId = {agentId}");
            return;
        } 

        //cell.scents[sIdx].airIntensity += airAmount * airScentDepositRate;
        //cell.scents[sIdx].groundIntensity += groundAmount * groundScentDepositRate;
 
        cell.scents[sIdx].airNextDelta += airAmount * airScentDepositRate;
        cell.scents[sIdx].groundNextDelta += groundAmount * groundScentDepositRate;
        // add cell to the list if it isn't there already
        if (!scentCells.Contains(cell)) {
            scentCells.Add(cell);
            //Debug.Log($"Added scent cell at {cell.pos} for agent {agentId}");
        }
    }

    /// <summary>
    /// Helper to query total scent for AI (weighted blend of air + ground).
    /// </summary>
    public float GetCombinedScent(Cell cell, int agentId, float groundWeight = 0.7f, float airWeight = 0.3f)
    {
        if (cell == null || cell.scents == null) return 0f;
        int sIdx = FindAgentIdScentIndex(cell, agentId, createIfNeeded: false);
        if (sIdx < 0) return 0f;

        return groundWeight * cell.scents[sIdx].groundIntensity +
               airWeight * cell.scents[sIdx].airIntensity;
    }

    // Returns the offset in the cell.scents array that contains the agentId.  Returns -1 if not found.
    public int FindAgentIdScentIndex(Cell cell, int agentId, bool createIfNeeded = true)
    {
        // Determine Agent's scent index
        Agent agent;
        int sIdx;
        if (cell == null) return -1;    // cannot do anything if the cell doesn't exist.
        if (cell.scents == null)
        {
            if (createIfNeeded)
                cell.scents = new();   // create the list if not present.
            else
                return -1;
        }
        for (sIdx = cell.scents.Count - 1; sIdx >= 0; sIdx--)    // search the list last to first = optimize for recent scents
        {
            //Debug.Log($"find agentId {agentId}: sidx={sIdx} of {cell.scents.Count} agentId={cell.scents[sIdx].agentId}");
            if (cell.scents[sIdx].agentId == agentId) break;
        }
        if ((sIdx == -1) && createIfNeeded)
        {
            // before creating new scent, we need the agent pointer
            agent = GetAgentFromAgentId(agentId);

            ScentClass newScent = new()
            {
                agentId = agentId,
                agent = agent
            };
            cell.scents.Add(newScent);
            sIdx = cell.scents.Count - 1;
        }
        return sIdx;
    }

    // returns a pointer to the Agent matching agentId
    Agent GetAgentFromAgentId(int agentId)
    {
        Agent agent;
        if ((dir.gen.agentRegistry == null) || (dir.gen.agentRegistry.Count-1 <= agentId) || (agentId<1))
        {
            Debug.LogError($"AgentRegistry doesn't include agentId={agentId}, max={dir.gen.agentRegistry.Count}");
            return null;
        }

        agent = dir.gen.agentRegistry[agentId-1];   // (index zero is agent 1)
        if (agent==null)
        {
            Debug.LogError($"AgentRegistry returned null at agentId={agentId}");
        }
        return agent;
    }

    // adds the cell to the current big scent list if it isn't already there.
    void AddToScentCellsList(Cell cell)
    {
        if (!scentCells.Contains(cell))
            scentCells.Add(cell);
    }


    public void ActivateOverlayForSource(ScentSource source)
    {
        // 1) Hide all existing fog by setting alpha to 0 for every instance.
        ClearAllScentVisuals();     // Is this necessary if we do final step below?

        // 2) Switch currently visualized agent
        if ((source == null) || (source.agentId == -1))
        {
            currentAgentId = -1;    // this means none.
            // Next Update(), the scent camera will turn off'
            // so nothing more to do here.
            return; 
        }
        else
        {
            currentAgentId = source.agentId;

            // 3) Set base colors for this scent
            airBaseColor    = source.sourceAirColor;
            groundBaseColor = source.sourceGroundColor;

            //groundScentVisible = true;
            //airScentVisible = true;
        }
        // 4) Force at least one visualization pass so new fog appears immediately
        ForceFullVisualizationRefresh();
    }

    private void ForceFullVisualizationRefresh()
    {
        // Easiest way: just run one StepOnce with dt=0 to push colors
        // or set a flag that your ScentUpdate coroutine checks:
        // e.g., needsFullRefresh = true;

        // If you want immediate, you can call:
        //ScentPhysicsStepOnce(0.0f);
        VisualizeCurrentScents();
    }    

    /// <summary>
    /// Collect scents present in the given cell, sorted strongest->weakest.
    /// This will be called by the 'sniff' command to populate a list for the player.
    /// </summary>
    public List<ScentDetection> CollectScentsAtCell(Cell cell)
    {
        var results = new List<ScentDetection>();

        if (cell == null || cell.scents == null || cell.scents.Count == 0)
            return results;

        // You can promote these weights to fields on ScentRegistry if you want tweakables.
        const float airWeight    = 0.3f;
        const float groundWeight = 0.7f;

        foreach (var scent in cell.scents)
        {
            if (scent == null) continue;

            float air    = scent.airIntensity;
            float ground = scent.groundIntensity;

            // Skip truly empty scents
            if (air <= 0f && ground <= 0f)
                continue;

            // Lookup or create the ScentSource metadata for this agentId
            ScentSource source = dir.scentRegistry.GetScentSource(scent.agentId);
            if (source == null) continue;   // not a valid scent

            float combined = ground * groundWeight + air * airWeight;

            results.Add(new ScentDetection
            {
                source           = source,
                airStrength      = air,
                groundStrength   = ground,
                combinedStrength = combined
            });
        }

        // Sort strongest → weakest by combined strength
        results.Sort((a, b) => b.combinedStrength.CompareTo(a.combinedStrength));

        // BEGIN DEBUG
        Debug.Log($"CollectScetsAtCell({cell.pos}) found {results.Count} scents:");
        foreach (ScentDetection s in results)
        {
            Debug.Log($"::{s.source.scentName} = {s.airStrength} + {s.groundStrength} -> {s.combinedStrength}");
        }
        // END DEBUG

        return results;
    }

    /// <summary>
    /// Immediately hides all scent visuals (air + ground) by setting alpha to 0.
    /// Does not destroy any instances; they remain in ElementStore/Warehouse pools.
    /// Scent structures are also updated to reflect zero visualization.
    /// </summary>
    public void ClearAllScentVisuals()
    {
        if (elementStore == null) return;

        // Clear Air layer
        var airLayer = elementStore.GetLayer(ElementLayerKind.ScentAir);
        if (airLayer != null && airLayer.instances != null)
        {
            for (int i = 0; i < airLayer.instances.Count; i++)
            {
                var inst = airLayer.instances[i];
                var c = inst.color;
                c.a = 0f;
                inst.color = c;
                inst.dirtyFlags |= ElementUpdateFlags.Color;
                airLayer.instances[i] = inst;
            }
        }

        // Clear Ground layer
        var groundLayer = elementStore.GetLayer(ElementLayerKind.ScentGround);
        if (groundLayer != null && groundLayer.instances != null)
        {
            for (int i = 0; i < groundLayer.instances.Count; i++)
            {
                var inst = groundLayer.instances[i];
                var c = inst.color;
                c.a = 0f;
                inst.color = c;
                inst.dirtyFlags |= ElementUpdateFlags.Color;
                groundLayer.instances[i] = inst;
            }
        }
        
        // Also reset last visualized values in all scents to zero
        int cIdx;
        int sIdx;
        Cell cell;
        ScentClass scent;
        for (cIdx = 0; cIdx < scentCells.Count; cIdx++)
        {
            cell = scentCells[cIdx];
            if (cell == null) continue;

            if (cell.scents==null || cell.scents.Count==0) continue;    // no scents exists for this cell

            for (sIdx = 0; sIdx < cell.scents.Count; sIdx++)
            {
                scent = cell.scents[sIdx];
                // reset last visualized values to match zero alpha we just set
                scent.airLastVisualized = 0f;
                scent.groundLastVisualized = 0f;
            }
        }

        // Reset change/creation flags since we just applied them here.
        anyScentAirChanged = false;
        anyScentGroundChanged = false;
        anyScentAirCreated = false;
        anyScentGroundCreated = false;
    }

    #endregion
}