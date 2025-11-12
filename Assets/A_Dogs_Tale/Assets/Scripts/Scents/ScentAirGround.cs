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
    public List<Cell> scentCells = new List<Cell>(); // Cache a list including only cells with AgentId's scent
    private int currentAgentId = -1;      // if this changes, recreate above list...

    [Header("Air Layer Settings")]
    [Range(0f, 1f)]
    public float airDiffusionRate = 0.15f;  // how strongly air mixes between neighbors per tick

    [Range(0f, 1f)]
    public float airDecayRate = 0.2f;       // per-second fraction of airborne scent lost

    [Header("Ground Layer Settings")]
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
        new Vector2Int( 1,  0),
        new Vector2Int(-1,  0),
        new Vector2Int( 0,  1),
        new Vector2Int( 0, -1),
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
        // Call this every time we change what we are looking for/visualizing...
        scentCells = CreateScentCellsForAgentList(currentAgentId);

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

        WaitForSeconds wait = new WaitForSeconds(simulationTimeStep);

        while (true)
        {
            StepOnce(simulationTimeStep);   // TODO: pass in actual time since last call
            yield return wait;
        }
    }

    /// <summary>
    /// One simulation step: diffusion, coupling, decay, and commit.
    /// </summary>
/*
    private void StepOnce_old(float dt)
    {
        if (scentCells == null || scentCells.Count == 0)
            return;

        float airDecayFactor = Mathf.Clamp01(1f - airDecayRate * dt);
        float groundDecayFactor = Mathf.Clamp01(1f - groundDecayRate * dt);
        float depositFactor = airToGroundRate * dt;
        float emitFactor = groundToAirRate * dt;
        int sIdx;

        // FIRST PASS: compute airNextDelta and groundNextDelta for each cell
        for (int index = 0; index < scentCells.Count; index++)
        {
            Cell cell = scentCells[index];
            if (cell == null) continue;

            sIdx = FindAgentIdScentIndex(cell, currentAgentId, createIfNeeded: false);
            if (sIdx < 0) Debug.LogError($"FindAgentIdScentIndex(cell {cell.pos.x},{cell.pos.y}, agentId={currentAgentId} returned not found.)");
            ScentClass scent = cell.scents[sIdx];
            if (scent == null) continue;

            float baseAir = scent.airIntensity;
            float baseGround = scent.groundIntensity;

            // Gather neighbor averages using the heightfield
            float airNeighborSum = 0f;
            float groundNeighborSum = 0f;
            int neighborCount = 0;

            // Get this cell's heightfield coordinates.
            // TODO: adjust how you access c.x, c.y, c.z (your HF coords).
            Vector3Int hf = cell.pos3d; // e.g. stored on your Cell; change as needed

            foreach (Vector2Int offset in neighborOffsets)  // TODO: Change to our direction to vector routine
            {
                int neighborX = hf.x + offset.x;
                int neighborY = hf.y + offset.y;
                int neighborZ = hf.z;

                Cell neighborCell = dir.gen.GetCellFromHf(
                    neighborX,
                    neighborY,
                    neighborZ,
                    threshold: 50); // same threshold you showed

                if (neighborCell == null) continue;

                int neighborSIdx = FindAgentIdScentIndex(neighborCell, currentAgentId, createIfNeeded: false);
                if (neighborSIdx < 0) continue;
                ScentClass neighborScent = neighborCell.scents[neighborSIdx];
                if (neighborScent == null) continue;

                neighborCount++;
                airNeighborSum += neighborScent.airIntensity;
                groundNeighborSum += neighborScent.groundIntensity;
            }

            float airValue = baseAir;
            float groundValue = baseGround;

            if (neighborCount > 0)
            {
                float airNeighborAverage = airNeighborSum / neighborCount;
                float groundNeighborAverage = groundNeighborSum / neighborCount;

                // Diffusion pulls intensity toward neighbor average
                airValue += airDiffusionRate * (airNeighborAverage - baseAir);
                groundValue += groundDiffusionRate * (groundNeighborAverage - baseGround);
            }

            // Phase coupling: air <-> ground
            float deposit = baseAir * depositFactor; // air -> ground
            float emit = baseGround * emitFactor;    // ground -> air

            airValue = (airValue - deposit) * airDecayFactor + emit;
            groundValue = (groundValue + deposit) * groundDecayFactor - emit;

            if (airValue < 0f) airValue = 0f;
            if (groundValue < 0f) groundValue = 0f;

            scent.airNextDelta = airValue;
            scent.groundNextDelta = groundValue;
        }

        // SECOND PASS: commit the new values and clear next-deltas
        for (int index = 0; index < scentCells.Count; index++)
        {
            Cell cell = scentCells[index];
            if (cell == null) continue;

            sIdx = FindAgentIdScentIndex(cell, currentAgentId, createIfNeeded: false);
            if (sIdx < 0) Debug.LogError($"FindAgentIdScentIndex(cell {cell.pos.x},{cell.pos.y}, agentId={currentAgentId} returned not found.)");
            ScentClass scent = cell.scents[sIdx];
            if (scent == null) continue;

            scent.airIntensity = scent.airNextDelta;
            scent.groundIntensity = scent.groundNextDelta;

            scent.airNextDelta = 0f;
            scent.groundNextDelta = 0f;
        }
    }
*/
    private void StepOnce(float dt)
    {
        if (scentCells == null || scentCells.Count == 0)
            return;

        float airDecayFactor    = Mathf.Clamp01(1f - airDecayRate    * dt);
        float groundDecayFactor = Mathf.Clamp01(1f - groundDecayRate * dt);
        float depositFactor     = airToGroundRate * dt;
        float emitFactor = groundToAirRate * dt;

        int sIdx;
        int n_sIdx;
        // FIRST PASS: compute deltas and accumulate into airNextDelta / groundNextDelta
        for (int index = 0; index < scentCells.Count; index++)
        {
            Cell cell = scentCells[index];
            if (cell == null) continue;

            sIdx = FindAgentIdScentIndex(cell, currentAgentId, createIfNeeded: false);
            if (sIdx < 0) continue;

            ScentClass scent = cell.scents[sIdx];  // TODO: adjust accessor
            if (scent == null) continue;

            float baseAir    = scent.airIntensity;
            float baseGround = scent.groundIntensity;

            // Gather neighbor averages using heightfield
            float airNeighborSum    = 0f;
            float groundNeighborSum = 0f;
            int neighborCount       = 0;

            Vector3Int hf = cell.pos3d;

            foreach (Vector2Int offset in neighborOffsets)
            {
                int neighborX = hf.x + offset.x;
                int neighborY = hf.y + offset.y;
                int neighborZ = hf.z;

                Cell neighborCell = dir.gen.GetCellFromHf(
                    neighborX,
                    neighborY,
                    neighborZ,
                    threshold: 50);

                if (neighborCell == null) continue;

                n_sIdx = FindAgentIdScentIndex(cell, currentAgentId, createIfNeeded: false);
                if (n_sIdx < 0) continue;
                ScentClass neighborScent = neighborCell.scents[n_sIdx];
                if (neighborScent == null) continue;

                neighborCount++;
                airNeighborSum    += neighborScent.airIntensity;
                groundNeighborSum += neighborScent.groundIntensity;
            }

            float airValue    = baseAir;
            float groundValue = baseGround;

            if (neighborCount > 0)
            {
                float airNeighborAverage    = airNeighborSum    / neighborCount;
                float groundNeighborAverage = groundNeighborSum / neighborCount;

                // Diffusion moves towards neighbor average
                airValue    += airDiffusionRate    * (airNeighborAverage    - baseAir);
                groundValue += groundDiffusionRate * (groundNeighborAverage - baseGround);
            }

            // Phase coupling: air <-> ground
            float deposit = baseAir    * depositFactor; // air -> ground
            float emit    = baseGround * emitFactor;    // ground -> air

            airValue    = (airValue    - deposit) * airDecayFactor + emit;
            groundValue = (groundValue + deposit) * groundDecayFactor - emit;

            if (airValue < 0f)    airValue    = 0f;
            if (groundValue < 0f) groundValue = 0f;

            // Convert to delta relative to current intensity and accumulate
            float airDelta    = airValue    - baseAir;
            float groundDelta = groundValue - baseGround;

            scent.airNextDelta    += airDelta;
            scent.groundNextDelta += groundDelta;
        }

        // SECOND PASS: visualization + conditional commit of nextDelta -> intensity
        for (int index = 0; index < scentCells.Count; index++)
        {
            Cell cell = scentCells[index];
            if (cell == null) continue;

            sIdx = FindAgentIdScentIndex(cell, currentAgentId, createIfNeeded: false);
            if (sIdx < 0) continue;

            ScentClass scent = cell.scents[sIdx];
            if (scent == null) continue;

            ScentVisualization(cell, scent);
        }
    }

    private void ScentVisualization(Cell cell, ScentClass scent)
    {
        if (elementStore == null) return;

        float airDelta    = scent.airNextDelta;
        float groundDelta = scent.groundNextDelta;

        bool airChanged    = Mathf.Abs(airDelta)    >= scentVisualThreshold;
        bool groundChanged = Mathf.Abs(groundDelta) >= scentVisualThreshold;

        // If neither layer changed enough, skip everything:
        // - no intensity update
        // - no visual update
        // nextDelta will continue to accumulate in future steps.
        if (!airChanged && !groundChanged)
            return;

        // ----- Apply deltas to intensities -----

        if (airChanged)
        {
            scent.airIntensity += airDelta;
            if (scent.airIntensity < 0f) scent.airIntensity = 0f;
            scent.airNextDelta = 0f;
        }

        if (groundChanged)
        {
            scent.groundIntensity += groundDelta;
            if (scent.groundIntensity < 0f) scent.groundIntensity = 0f;
            scent.groundNextDelta = 0f;
        }

        // ----- Update / create visuals -----

        // =====Air layer
        if (scent.airIntensity > 0f)
        {
            float normalized = Mathf.Clamp01(scent.airIntensity / maxVisualIntensity);
            Color color = airBaseColor;
            color.a = normalized;

            if (scent.airGOindex < 0)
            {
                // First time: create the ScentAir element and store its index
                // TODO: adjust AddScentAir signature as needed.
                scent.airGOindex = elementStore.AddScentAir(cell, color);
            }
            else
            {
                // Update color of existing instance
                elementStore.ChangeColor(ElementLayerKind.ScentAir, scent.airGOindex, cell, color);
            }
        }
        else if (scent.airGOindex >= 0)
        {
            // Optionally fade out / hide when intensity hits zero
            // (e.g. set alpha to 0 or destroy the element).
            // For now we just make it fully transparent via ChangeColor.
            Color color = airBaseColor;
            color.a = 0f;
            elementStore.ChangeColor(ElementLayerKind.ScentAir, scent.airGOindex, cell, color);
        }

        // =======Ground layer
        if (scent.groundIntensity > 0f)
        {
            float normalized = Mathf.Clamp01(scent.groundIntensity / maxVisualIntensity);
            Color color = groundBaseColor;
            color.a = normalized;

            if (scent.groundGOindex < 0)
            {
                // First time: create the ScenGround element and store its index
                // TODO: adjust AddScentAir signature as needed.
                scent.groundGOindex = elementStore.AddScentGround(cell, color);
            }
            else
            {
                // Update color of existing instance
                elementStore.ChangeColor(ElementLayerKind.ScentGround, scent.groundGOindex, cell, color);
            }
        }
        else if (scent.groundGOindex >= 0)
        {
            // Optionally fade out / hide when intensity hits zero
            // (e.g. set alpha to 0 or destroy the element).
            // For now we just make it fully transparent via ChangeColor.
            Color color = groundBaseColor;
            color.a = 0f;
            elementStore.ChangeColor(ElementLayerKind.ScentGround, scent.groundGOindex, cell, color);
        }
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
        if (sIdx < 0) return;

        cell.scents[sIdx].airIntensity += airAmount;
        cell.scents[sIdx].groundIntensity += groundAmount;
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
        if (cell.scents == null && createIfNeeded) cell.scents = new();   // create the list if not present.

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


    #endregion
}