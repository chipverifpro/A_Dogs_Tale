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
    //public List<Cell> agentScentCells = new List<Cell>(); // Cache a list including only cells with current AgentId's scent
    
    public int currentAgentId = -1;      // if this changes, recreate above list...

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

    public void StartScentSimulation()
    {
        if (_simulationCoroutine != null)
        {
            StopCoroutine(_simulationCoroutine);    // kill previously still running copy?
        }

        currentAgentId = 1;
        // Call this every time we change map structure (load/build complete): likely to start empty or nearly so before scents start appearing.
        ScentCellsListCreate();

        // Call this every time we change agentId that we are visualizing...
        //agentScentCells = new(); 
        //AgentScentCellsListCreate(currentAgentId);

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

    /* DON"T NEED THIS ???
    // This list is all cells with scents from agentId in them.  Used to speed up scent visualization.
    public void AgentScentCellsListCreate(int agentId)
    {
        agentScentCells = new();
        foreach (Room room in dir.gen.rooms)
        {
            foreach (Cell cell in room.cells)
            {
                if (cell.scents == null) continue;
                if (cell.scents.Count == 0) continue;
                for (int sIdx = 0; sIdx < cell.scents.Count; sIdx++)
                {
                    if (cell.scents[sIdx].agentId == agentId)
                        agentScentCells.Add(cell);
                }

            }
        }
    } */

    /// <summary>
    /// Main coroutine that steps the scent field over time using heightfield neighbors.
    /// </summary>
    private IEnumerator ScentDecayAndSpread()
    {
        if (dir == null || dir.gen == null)
        {
            Debug.LogError("ScentSystem: dir or dir.gen reference is missing.");
            yield break;
        }

        // Ensure the heightfield is ready
        if (!dir.gen.hf_valid || dir.gen.hf == null || dir.gen.hf.Width == 0 || dir.gen.hf.Height == 0)
        {
            Debug.Log("ScentDecayAndSpread: Heightfield not valid, preparing heightfield.");
            dir.gen.PrepareHeightfield();
            yield return null;
        }

        // Wait for build complete.
        if (dir.gen.buildComplete == false)
            yield return new WaitUntil(() => dir.gen.buildComplete == true);    // wait until build is done

        // Call this every time we change map structure (load/build complete): likely to start empty or nearly so before scents start appearing.
        ScentCellsListCreate();
        
        WaitForSeconds wait = new WaitForSeconds(simulationTimeStep);
        
        if (dir.cfg.scentPhysicsConsistancey == false) // option A is to scale the call by actual time step
        {
            while (true)
            {
                StepOnce(Time.deltaTime);
                ApplyScentUpdates();
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

                // Run one or more steps to catch up
                while (accumulator >= step)
                {
                    StepOnce(step);
                    accumulator -= step;
                }
                ApplyScentUpdates();

                yield return null;
            }
        }

    }

    /// <summary>
    /// One simulation step: diffusion, coupling, decay, and commit.
    /// </summary>
    private void StepOnce(float dt)
    {
        if (scentCells == null || scentCells.Count == 0)
            scentCells = new(); // create it empty.  could return now if we want
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
        Cell[] neighbors = new Cell[4]; // cache the 4 neighbor cells once per cell examined.  reuse on each scent.  Change to 8 if you want diagonals.
        int neighborCount;
        int original_cell_count = scentCells.Count;

        // FIRST PASS: compute deltas and accumulate into airNextDelta / groundNextDelta

        // This loop doesnt include any extra cells we add this pass.
        for (cIdx = 0; cIdx < original_cell_count; cIdx++)
        {
            Cell cell = scentCells[cIdx];
            if (cell == null) continue; // should not happen

            // quick check to see if any scents are non-zero
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
                if (i_have_scents && (neighbors[nIdx] != null) && !scentCells.Contains(neighbors[nIdx]))
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
                                neighbors[nIdx].scents.Add(newScent);  // add scent to neighbor cell
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

        // SECOND PASS: conditional comit of nextDelta -> intensity
        //              visualization if matches currentAgentId
        
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
                float airDelta    = scent.airNextDelta;
                float groundDelta = scent.groundNextDelta;

                bool airChanged;
                bool groundChanged;

                // only limit VisualThreshold for the visualualized currentAgentId
                if (scent.agentId == currentAgentId)
                {
                    airChanged = Mathf.Abs(airDelta) >= scentVisualThreshold;
                    groundChanged = Mathf.Abs(groundDelta) >= scentVisualThreshold;
                }
                else // not the visualized currentAgentId
                {
                    airChanged = airDelta != 0;
                    groundChanged = groundDelta != 0;
                }

                // If either layer did not change enough, skip propagation:
                // - no intensity update
                // - no visual update
                // nextDeltas left behind will continue to accumulate in future steps.

                // ----- Apply deltas to intensities ONLY IF change exceeds scentVisualThreshold -----

                if (airChanged)
                {
                    scent.airIntensity += airDelta;
                    if (scent.airIntensity < practically_zero) scent.airIntensity = 0f;
                    scent.airNextDelta = 0f;
                }

                if (groundChanged)
                {
                    scent.groundIntensity += groundDelta;
                    if (scent.groundIntensity < practically_zero) scent.groundIntensity = 0f;
                    scent.groundNextDelta = 0f;
                }

                // ONLY visualize if this is true...
                if (((airChanged || groundChanged)  && scent.agentId == currentAgentId) 
                     || (airScentVisible != airScentWasVisible)         // user toggled air on/off
                     || (groundScentVisible != groundScentWasVisible))  // user toggled ground on/off
                {
                    ScentVisualization(cell, scent);
                }
            }
        }
    }

    private void ScentVisualization(Cell cell, ScentClass scent)
    {
        Color color;
        float normalized;
        bool airUpdateAll = true;
        bool groundUpdateAll = true;

        if (elementStore == null) return;
        //Debug.Log($"Visualize scent at cell {cell.pos}.  Air = {scent.airIntensity}, Ground = {scent.groundIntensity}");

        // ----- Create or update visuals -----

        // =====Air layer
        if (airScentVisible != airScentWasVisible)    // airVisible was just toggled on or off
        {
            airUpdateAll = true;
            airScentWasVisible = airScentVisible;
        }

        if (airScentVisible || airUpdateAll)        // we should update
        {
            if ((scent.airIntensity > 0f) && airScentVisible)
            {
                normalized = Mathf.Clamp01(scent.airIntensity) * maxVisualIntensity;
                color = airBaseColor;
                color.a = normalized;

                if (scent.airGOindex < 0) // intensity>0 and no GO exists
                {
                    // First time: create the ScentAir element and store its index
                    scent.airGOindex = elementStore.AddScentAir(cell, color);

                    if (scent.airGOindex < 0)
                        Debug.LogError($"ScentVisualization: AddScentAir(@{cell.pos}, alpha={color.a}) returned -1");
                }
                else // intensity>0 and GO already exists
                {
                    // Update color of existing instance
                    elementStore.ChangeColor(ElementLayerKind.ScentAir, scent.airGOindex, cell, color);
                }
            }
            else if (scent.airGOindex >= 0)     // intensity==0 and GO exists OR !visible
            {
                // Future: Optionally fade out / hide when intensity hits zero
                //         (e.g. set alpha to 0 or destroy the element).
                // For now we just make it fully transparent via ChangeColor.
                color = airBaseColor;
                color.a = 0f;
                elementStore.ChangeColor(ElementLayerKind.ScentAir, scent.airGOindex, cell, color);
            }
        }
        // =======Ground layer
        if (groundScentVisible != groundScentWasVisible)    // airVisible was just toggled on or off
        {
            groundUpdateAll = true;
            groundScentWasVisible = groundScentVisible;
        }

        if (groundScentVisible || groundUpdateAll)
        {
            if ((scent.groundIntensity > 0f) && groundScentVisible)
            {
                normalized = Mathf.Clamp01(scent.groundIntensity) * maxVisualIntensity;
                color = groundBaseColor;
                color.a = normalized;

                if (scent.groundGOindex < 0) // intensity>0 and no GO exists
                {
                    // First time: create the ScenGround element and store its index
                    scent.groundGOindex = elementStore.AddScentGround(cell, color);

                    if (scent.groundGOindex < 0)
                        Debug.LogError($"ScentVisualization: AddScentGround(@{cell.pos}, alpha={color.a}) returned -1");

                }
                else // intensity>0 and GO already exists
                {
                    // Update color of existing instance
                    elementStore.ChangeColor(ElementLayerKind.ScentGround, scent.groundGOindex, cell, color);
                }
            }
            else if (scent.groundGOindex >= 0)     // intensity==0 and GO exists OR !visible
            {
                // Future: Optionally fade out / hide when intensity hits zero
                //         (e.g. set alpha to 0 or destroy the element).
                // For now we just make it fully transparent via ChangeColor.
                color = groundBaseColor;
                color.a = 0f;
                elementStore.ChangeColor(ElementLayerKind.ScentGround, scent.groundGOindex, cell, color);
            }
        }
    }

    public void ApplyScentUpdates()
    {
        bool anyFogCreated = true;      // for debug
        bool anyColorChanged = true;    // for debug

        // If we created any new fog instances, manufacture GOs for them
        if (anyFogCreated)
        {
            dir.manufactureGO.BuildNewInstancesForLayer(ElementLayerKind.ScentAir);
            dir.manufactureGO.BuildNewInstancesForLayer(ElementLayerKind.ScentGround);
        }

        // Then apply all color changes (including newly created ones)
        if (anyFogCreated || anyColorChanged)
        {
            dir.manufactureGO.ApplyPendingUpdates();
        }

        //Debug.Log($"ApplyScentUpdates completed. anyColorChanged={anyColorChanged}, anyFogCreated={anyFogCreated}");
    }

    #region Public API

    /// <summary>
    /// Add scent to a specific cell (e.g., from an emitter).
    /// </summary>
    public void AddScentToCell(
        Cell cell,
        int agentId,
        float airAmount,
        float groundAmount = 0f)
    {
        int sIdx = FindAgentIdScentIndex(cell, agentId, createIfNeeded: true);
        //Debug.Log($"Adding scent agentId={agentId} to cell at {cell.pos}. sIdx={sIdx}");
        if (sIdx < 0) return;

        cell.scents[sIdx].airIntensity += airAmount;
        cell.scents[sIdx].groundIntensity += groundAmount;

        // add cell to the list if it isn't there already
        if (!scentCells.Contains(cell))
            scentCells.Add(cell);
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
        int sIdx;
        if (cell == null) return -1;    // cannot do anything if the cell doesn't exist.
        if (cell.scents == null)
        {
            if (createIfNeeded) cell.scents = new();   // create the list if not present.
            else return -1;
        }
        for (sIdx = cell.scents.Count - 1; sIdx > 0; sIdx--)    // search the list last to first = optimize for recent scents
            if (cell.scents[sIdx].agentId == agentId) break;

        if (sIdx == -1 && createIfNeeded)
        {
            // create new scent for agentId
            ScentClass newScent = new()
            {
                agentId = agentId
            };
            cell.scents.Add(newScent);
            sIdx = cell.scents.Count - 1;
        }
        return sIdx;
    }

    // Unused
    List<Cell> CreateScentCellsForAgentList(int agentId)
    {
        List<Cell> clist = new();
        currentAgentId = agentId;         // update
        if (agentId < 0) return clist;  // empty list
        
        // build list by scanning all cells in world, and keeping a list of the ones containing scent from the agentId
        foreach (Room room in dir.gen.rooms)
        {
            foreach (Cell cell in room.cells)
            {
                if (cell.scents == null) continue;
                if (cell.scents.Count == 0) continue;
                for (int idx = cell.scents.Count - 1; idx >= 0; idx--)
                    if (cell.scents[idx].agentId == agentId)
                        clist.Add(cell);
            }
        }
        return clist;
    }

    // adds the cell to the current big scent list if it isn't already there.
    void AddToScentCells(Cell cell)
    {
        if (!scentCells.Contains(cell))
            scentCells.Add(cell);
    }

    #endregion
}