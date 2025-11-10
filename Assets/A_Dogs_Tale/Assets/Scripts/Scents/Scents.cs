using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public partial class Scents : MonoBehaviour
{
    [Header("Directory Object")]
    public ObjectDirectory dir;     // Reference to external classes is maintained here
    public DungeonSettings cfg;     // This is used lots of places!

    public void Start()
    {
        // Defaults were getting messed up, so re-set them here.  Fixed?
        //cfg.ScentInterval = 10f;       // interval to decay/spread scents (seconds)
        //cfg.ScentDecayRate = 0.1f;     // decay by percent per ScentInterval
        //cfg.ScentSpreadAmount = 0.05f; // neighbors get this percent added per ScentInterval
        //cfg.ScentMinimum = 0.001f;     // amount below which the scent completely disappears

        StartCoroutine(ScentDecayOnIntervals());
    }

    public void Update()
    {

    }

    // This routine runs forever, one iteration at most every cfg.ScentInterval seconds.
    public IEnumerator ScentDecayOnIntervals()
    {

        yield return new WaitUntil(() => dir.gen.buildComplete == true);    // wait until build is done
        Debug.Log("ScentDecayOnIntervals: Dungeon build complete, starting scent decay routine.");
        dir.gen.PrepareHeightfield();

        float decay_routine_start_time = Time.time;
        float time_since_previous = 0f;
        int updatecnt = 0;
        while (true)
        {
            if (time_since_previous < cfg.scentInterval)
            {
                //Debug.Log($"ScentDecayOnIntervals waiting for {cfg.scentInterval - time_since_previous} seconds.");
                yield return new WaitForSeconds(cfg.scentInterval - time_since_previous);
                time_since_previous = Time.time - decay_routine_start_time;
            }

            decay_routine_start_time = Time.time;
            //Debug.Log($"Starting ScentDecayAndSpread at {decay_routine_start_time}, {time_since_previous} seconds since previous call. cfg.scentInterval={cfg.scentInterval}");
            yield return StartCoroutine(ScentDecayAndSpread(time_since_previous));
            time_since_previous = Time.time - decay_routine_start_time;
            //Debug.Log($"Finished ScentDecayAndSpread at {Time.time}, {time_since_previous} seconds since previous call. cfg.scentInterval={cfg.scentInterval}");

            // move this elsewhere?
            ScentFogUpdate(dir.player.agent.pos2, 1.0f);
            updatecnt++;
            if(updatecnt % 10 == 0)
            {
                //dir.manufactureGO.BuildAll();
            }
        }
    }

    // This routine is called approximately every cfg.scentInterval (maybe longer if previous iteration took long)
    //   It scans every Cell in every Room looking for existing scents.
    //   It decays scent by cfg.scentDecayRate.
    //   It spreads scent to each neighbor by cfg.scentSpreadAmount.
    //   All of these are put into a nextScent list so we only work on the originals.
    //     It moves all the nextScent list entries to the scent list except...
    //       It removes the scent if it decays below cfg.scentMinimum
    public IEnumerator ScentDecayAndSpread(float time_since_previous)
    {
        Cell neighborCell;
        Vector2Int nPos;
        int spread_count;
        int rooms_per_yield = 100;   // adjust as needed
        int room_yield_counter = rooms_per_yield; // countdown to zero to yield
        int yields = 0;

        // Build the heighfield hf if it doesn't yet exist.
        if (!dir.gen.hf_valid || dir.gen.hf == null || dir.gen.hf.Width == 0 || dir.gen.hf.Height == 0)
        {
            Debug.Log("ScentDecayAndSpread: Heightfield not valid, preparing heightfield.");
            dir.gen.PrepareHeightfield();
            yield return null;
        }

        if ((time_since_previous / cfg.scentInterval) > 1.25f)
        {
            Debug.LogWarning($"ScentDecayAndSpread called after {time_since_previous} seconds, which is significantly longer than expected interval of {cfg.scentInterval} seconds.");
        }
        float scaled_spread_amount = cfg.scentSpreadAmount * (time_since_previous / cfg.scentInterval);
        float scaled_decay_rate = cfg.scentDecayRate * (time_since_previous / cfg.scentInterval);
        Debug.Log($"ScentDecayAndSpread starting with time_since_previous={time_since_previous}, scaled_spread_amount={scaled_spread_amount}, scaled_decay_rate={scaled_decay_rate}.");
        // Check for problems at long intervals
        if (scaled_spread_amount > 0.2f)
        {
            Debug.LogError($"ScentDecayAndSpread: scaled_spread_amount {scaled_spread_amount} is too high and will likely cause complete collapse of scent algorithm.");
            scaled_spread_amount = 0.2f; // clamp it, but this only masks the real problem.
            // result at 1/4th is that original scent disappears in one iteration.
            // result at 1/5th is that scent in adjacent cells becomes greater than original in one iteration, making tracking impossible.
            // is 1/6th safe?  need to analyze more.  I'd feel more comfortable at 1/10th.
        }
        yield return null;
        if (!dir.gen.buildComplete) yield break;    // can't do scents until build is done.

        foreach (Room r in dir.gen.rooms)
        {
            foreach (Cell c in r.cells)
            {
                if (c.scents != null)
                {
                    for (int s = 0; s < c.scents.Count; s++)
                    {
                        if (c.scents[s].intensity == 0f) continue; // skip zero-intensity scents
                        if (c.scents[s].intensity < 0f)
                        {
                            Debug.LogError($"({c.x},{c.y},{c.z}) ScentDecayAndSpread found negative scent intensity {c.scents[s].intensity} for agentId {c.scents[s].agentId} in cell ({c.x},{c.y},{c.z}). Setting to zero.");
                            c.scents[s].intensity = 0f;
                            continue;
                        }
                        float orig_intensity = c.scents[s].intensity;
                        spread_count = 0;
                        float spread_amount_per_direction = orig_intensity * scaled_spread_amount;
                        float accumulatedScentSpread = 0f;
                        Debug.Log($"({c.x},{c.y},{c.z})     ScentDecayAndSpread processing cell scent agentId {c.scents[s].agentId} with orig_intensity {orig_intensity}, spread_amount_per_direction {spread_amount_per_direction}.");
                        // spread scent to nearby cells
                        foreach (DirFlags dirFlags in DirFlagsEx.AllCardinals)
                        {
                            //walls and closed doors block scent spread
                            if (((dirFlags & c.walls) == 0) && ((dirFlags & c.doors) == 0))    // TODO: include door open/closed
                            {
                                nPos = DirFlagsEx.ToVector2Int(dirFlags);
                                neighborCell = dir.gen.GetCellFromHf(c.x + nPos.x, c.y + nPos.y, c.z, threshold: 50);
                                Debug.Log($"({c.x},{c.y},{c.z})   Checking neighbor {neighborCell} at direction {dirFlags} position ({c.x + nPos.x},{c.y + nPos.y},{c.z}).");
                                if (neighborCell != null)
                                {
                                    Debug.Log($"({c.x},{c.y},{c.z})     Neighbor cell found at ({neighborCell.x},{neighborCell.y},{neighborCell.z}). Spreading scent = {spread_amount_per_direction}.");
                                    // spread the scent...
                                    AddToNextScentIntensity(neighborCell, c.scents[s].agentId, spread_amount_per_direction);
                                    accumulatedScentSpread += spread_amount_per_direction;
                                    spread_count++;
                                }
                            }
                        }
                        // to decay this scent, add a negative amount
                        //   start with original intensity (orig_intensity)
                        //   subtract amount spread to neighbors (each neighbor got cfg.ScentSpreadAmount * orig_intensity)
                        //   decay the remaining scent by cfg.ScentDecayRate
                        //   
                        //  Note: removed a factor of openness from decay since we are already blocking spread with walls/doors.
                        float scent_after_spread = orig_intensity - accumulatedScentSpread;
                        float decay_remaining_scent = scent_after_spread * scaled_decay_rate;
                        float delta = -accumulatedScentSpread - decay_remaining_scent;
                        Debug.Log($"({c.x},{c.y},{c.z})     ScentDecay for agentId {c.scents[s].agentId}: orig_intensity={orig_intensity}, accumulatedScentSpread={accumulatedScentSpread} over {spread_count} neighbors, scent_after_spread={scent_after_spread}, decay_remaining_scent={decay_remaining_scent}, total delta to apply={delta}, final = {orig_intensity + delta}.");
                        AddToNextScentIntensity(c, c.scents[s].agentId, delta);
                    }
                }
            }
        }
        Debug.Log($"ScentDecayAndSpread completed spreading scents. Now adding NextDeltas to Scents.");
        // now, transfer all those NextScents back to Scents.
        foreach (Room room in dir.gen.rooms)
        {
            foreach (Cell cell in room.cells)
            {
                if (cell.scents == null) continue; //cell.scents = new();
                for (int scent_num = cell.scents.Count - 1; scent_num >= 0; scent_num--)
                {
                    // only keep scent if above cfg.scentMinimum
                    if (cell.scents[scent_num].intensity + cell.scents[scent_num].nextDelta >= cfg.scentMinimum)
                    {
                        Debug.Log($"({cell.x},{cell.y},{cell.z}) Updating scent for agent {cell.scents[scent_num].agentId}: intensity={cell.scents[scent_num].intensity}, nextDelta={cell.scents[scent_num].nextDelta}. New intensity={cell.scents[scent_num].intensity + cell.scents[scent_num].nextDelta}.");
                        cell.scents[scent_num].intensity += cell.scents[scent_num].nextDelta;
                        cell.scents[scent_num].nextDelta = 0; // clear for next pass
                    }
                    else
                    {
                        Debug.Log($"({cell.x},{cell.y},{cell.z}) Removing scent for agent {cell.scents[scent_num].agentId} due to low intensity: intensity={cell.scents[scent_num].intensity}, nextDelta={cell.scents[scent_num].nextDelta}. New intensity would be {cell.scents[scent_num].intensity + cell.scents[scent_num].nextDelta} which is below minimum {cfg.scentMinimum}.");
                        cell.scents.RemoveAt(scent_num);
                    }
                }
            }
        }

        // yield every N rooms to allow other activities
        room_yield_counter--;
        if (room_yield_counter <= 0)
        {
            room_yield_counter = rooms_per_yield;
            yields++;
            yield return null;
        }
        Debug.Log($"ScentDecayAndSpread completed after {yields} yields.");
    }

    public void AddToNextScentIntensity(Cell c, int agent_id, float? added_intensity = null, float? set_intensity = null)
    {
        //Debug.Log($"AddToNextScentIntensity called for agent {agent_id} in cell ({c.x},{c.y},{c.z}) with added_intensity={added_intensity}, set_intensity={set_intensity}.");
        if (c.scents == null) c.scents = new();
        //if (c.nextScents == null) c.nextScents = new();

        // if we find a matching agentId, add the scent amount
        for (int scent_num = 0; scent_num < c.scents.Count; scent_num++)
        {
            if (c.scents[scent_num].agentId == agent_id)
            {
                //Debug.Log($"AddToNextScentIntensity found existing scent for agent {agent_id} in cell ({c.x},{c.y},{c.z}). OLD intensity={c.scents[scent_num].intensity}, nextDelta={c.scents[scent_num].nextDelta}.");
                if (set_intensity.HasValue)
                {
                    c.scents[scent_num].intensity = set_intensity.Value;    // overwrite current intensity
                    // Used to place initial scent amounts
                    //Debug.Log($"({c.x},{c.y},{c.z}) AddToNextScentIntensity set intensity for agent {agent_id}. NEW intensity={c.scents[scent_num].intensity}, NEW nextDelta={c.scents[scent_num].nextDelta}.");
                }
                if (added_intensity.HasValue)
                {
                    c.scents[scent_num].nextDelta += added_intensity.Value;
                    Debug.Log($"({c.x},{c.y},{c.z}) AddToNextScentIntensity added delta for agent {agent_id}. NEW intensity={c.scents[scent_num].intensity}, NEW nextDelta={c.scents[scent_num].nextDelta}.");
                }

                //Debug.Log($"AddToNextScentIntensity updated scent for agent {agent_id} in cell ({c.x},{c.y},{c.z}). NEW intensity={c.scents[scent_num].intensity}, nextDelta={c.scents[scent_num].nextDelta}.");
                return;   // found a match so we are done.
            }
        }

        // matching AgentId not found, add a new scent to list;
        ScentClass new_scent = new ScentClass
        {
            agentId = agent_id,
            nextDelta = 0f,  //
            intensity = 0f,       // will be overwritten in next transfer
            fogIndex = -1
        };
        if (set_intensity.HasValue)
            new_scent.intensity = set_intensity.Value;    // overwrite current intensity
        if (added_intensity.HasValue)
            new_scent.nextDelta += added_intensity.Value;

        Debug.Log($"AddToNextScentIntensity adding NEW scent for agent {agent_id} in cell ({c.x},{c.y},{c.z}). NEW intensity={new_scent.intensity}, nextDelta={new_scent.nextDelta}.");
        c.scents.Add(new_scent);
    }

/*
    // This routine updates the scent fog visualization in each cell based on current scent amounts.
    // It will reuse existing fog GameObjects if they exist, or create new ones as needed.
    public void ScentFogUpdate_old(Vector3 nosePosition, float noseSensitivity)
    {
        //GameObject scentFogPrefab = dir.gen.floorPrefab;    // temporary; replace with actual scent fog prefab later
        Color colorScent = new Color(0.5f, 1f, 0.5f, 0.5f); // light greenish translucent
        bool changed = false;   // if anything changes, call update.
        bool created = false;   // if anything was created, call ?? update or create ??
        foreach (Room r in dir.gen.rooms)
        {
            foreach (Cell c in r.cells)
            {
                //                GameObject GO_fog = c.GOs[(int)Cell.GOtypes.Fog];    // grab existing fog object if it exists
                //                if (GO_fog == null)
                //                {
                //                    // create new fog object
                //                    GO_fog = Instantiate(scentFogPrefab, c.pos3d_f, Quaternion.Euler(90f, 0f, 0f), dir.gen.root);
                //                    GO_fog.transform.localScale = new Vector3(c.pos3d_f.x * 0.5f, c.pos3d_f.y * 0.5f, c.pos3d_f.z + 1f);
                //                    GO_fog.SetActive(false);            // hide until we need it
                //                    GO_fog.name = $"Fog({c.x},{c.y})";  // comment out in perf builds
                //                }
                // -------- Scent distance from nose --------
                float distFromNose = Vector3.Distance(nosePosition, c.pos3d_f);
                float scentDistanceFactor = 1;// Mathf.Clamp01(1f - (distFromNose / noseSensitivity));

                // -------- Scent fog visualization --------
                if (c.scents != null)
                {
                    int scent_id = 1; // only visualize dummy scent_id 1 for now
                    for (int scent_num = 0; scent_num < c.scents.Count; scent_num++)
                    {
                        Debug.Log($"({c.x},{c.y},{c.z}) ScentFogUpdate checking, found scent_id {c.scents[scent_num].agentId} with intensity {c.scents[scent_num].intensity}.");
                        float intensity = c.scents[scent_num].intensity * scentDistanceFactor;
                        if ((c.scents[scent_num].agentId == scent_id) && (intensity > 0.001f))
                        {
                            // found scent to visualize via transparency
                            float transparency = Mathf.Clamp01(intensity * 5f); // temporary scale factor for visibility
                            colorScent.a = transparency;

                            bool success = dir.elementStore.ChangeColor(
                                ElementLayerKind.Fog,
                                c,
                                colorScent
                            );
                            changed |= success;
                            if (!success)
                            {
                                Debug.Log($"({c.x},{c.y},{c.z}) ScentFogUpdate did not find existing fog GO, creating one at {c.pos3d_f}.");
                                // assume we failed because the GO did not exist yet.  Create one.
                                //Vector3 pos = new Vector3(c.pos3d_f.x * 0.5f, c.pos3d_f.y * 0.5f, c.pos3d_f.z + 1f);
                                Vector3 pos = new Vector3(c.pos3d_f.x, c.pos3d_f.z + 1f, c.pos3d_f.y); // y and z swapped for Unity coords
                                dir.elementStore.AddFog(
                                    archetypeId: "Fog",
                                    roomIndex: c.room_number,
                                    cellCoord: new Vector2Int(c.x, c.y),
                                    heightSteps: c.height,
                                    worldPos: pos,
                                    rotation: Quaternion.identity,
                                    scale: Vector3.one,
                                    color: colorScent
                                );
                            }
                            //Assign_GO_Color(GO_fog, colorScent, transparency);
                            //GO_fog.SetActive(true);
                        }
                        else
                        {
                            // no scent to visualize
                            colorScent.a = 0;   // alpha to zero
                            //GO_fog.SetActive(false);
                            bool success = dir.elementStore.ChangeColor(
                                ElementLayerKind.Fog,
                                c,
                                colorScent
                            );
                            created |= success;
                            // no need to create one if this failed to find it.
                        }
                    }
                }
                //c.GOs[(int)Cell.GOtypes.Fog] = GO_fog;   // save back to cell
            }
        }

        //if (changed || created)     // TODO: handle created separately?
        //{
        dir.manufactureGO.ApplyPendingUpdates();
        //}

        Debug.Log($"ScentFogUpdate completed. changed={changed}, created={created}");
    }
    */
    // This routine updates the scent fog visualization in each cell based on current scent amounts.
    // It will reuse existing fog instances if they exist in ElementStore, or create new ones as needed.
    public void ScentFogUpdate(Vector3 nosePosition, float noseSensitivity)
    {
        Color baseScentColor = new Color(0.5f, 1f, 0.5f, 0.5f); // light greenish translucent

        bool anyColorChanged = false;  // if any existing fog instance changed color
        bool anyFogCreated   = false;  // if any new fog instance was added to ElementStore

        const int scentIdToShow = 1;
        const float minVisibleIntensity = 0.001f;
        const float debugVisibilityScale = .75f;

        foreach (Room r in dir.gen.rooms)
        {
            foreach (Cell c in r.cells)
            {
                if (c.scents == null || c.scents.Count == 0)
                {
                    /* unnecessary and slow...
                    
                    // No scent here → clear any existing fog (set alpha to 0)
                    Color transparent = baseScentColor;
                    transparent.a = 0f;

                    bool changed = dir.elementStore.ChangeColor(
                        ElementLayerKind.Fog,
                        "Fog",
                        c,
                        -1,     // cell_scent_number unknown
                        transparent
                    );

                    anyColorChanged |= changed;
                    */
                    continue;
                }

                // -------- Scent distance from nose (you can re-enable distance factor later) --------
                float distFromNose = Vector3.Distance(nosePosition, c.pos3d_f);
                float scentDistanceFactor = 1f; // Mathf.Clamp01(1f - (distFromNose / noseSensitivity));
                int cell_scent_number = -1;

                // -------- Compute max intensity for this cell --------
                float maxIntensity = 0f;
                for (int i = 0; i < c.scents.Count; i++)
                {
                    var s = c.scents[i];
                    if (s.agentId != scentIdToShow)
                    {
                        continue;
                    }
                    cell_scent_number = i;
                    float intensity = s.intensity * scentDistanceFactor;
                    if (intensity > maxIntensity)
                        maxIntensity = intensity;
                }

                if (maxIntensity > minVisibleIntensity)
                {
                    float transparency = Mathf.Clamp01(maxIntensity * debugVisibilityScale);

                    Color fogColor = baseScentColor;
                    fogColor.a = transparency;

                    // Try to recolor an existing fog instance first
                    bool changed = dir.elementStore.ChangeColor(
                        ElementLayerKind.Fog,
                        "Fog",
                        c,
                        cell_scent_number,
                        fogColor
                    );

                    if (changed)
                    {
                        anyColorChanged = true;
                        if (cell_scent_number < 0) Debug.LogError($"Asumption was wrong.  color changed even unknown cell_scent_number.  If this never fires off, optimize above.");
                    } else {
                        // No existing fog instance for this cell → create one
                        Debug.Log($"({c.x},{c.y},{c.z}) ScentFogUpdate: no fog instance, creating one at {c.pos3d_f}.");

                        // World position for fog; adjust Y/Z as needed for your grid→world
                        //Vector3 pos = c.pos3d_f;
                        Vector3 pos = new Vector3(c.pos3d_f.x, c.pos3d_f.z + 1f, c.pos3d_f.y); // y and z swapped for Unity coords
                                
                        // Example: lift slightly above floor
                        pos.y += 0.1f;

                        dir.elementStore.AddFog(
                            archetypeId: "Fog",                       // must match an archetype in ElementStore
                            roomIndex: c.room_number,
                            cellCoord: new Vector2Int(c.x, c.y),
                            heightSteps: c.height,
                            worldPos: pos,
                            rotation: Quaternion.identity,
                            scale: Vector3.one,
                            color: fogColor
                        );

                        anyFogCreated = true;
                    }
                }
                else
                {
                    // Intensity too low → ensure fog here (if any) is transparent
                    Color transparent = baseScentColor;
                    transparent.a = 0f;

                    bool changed = dir.elementStore.ChangeColor(
                        ElementLayerKind.Fog,
                        "Fog",
                        c,
                        cell_scent_number,
                        transparent
                    );

                    anyColorChanged |= changed;
                }
            }
        }

        // If we created any new fog instances, manufacture GOs for them
        if (anyFogCreated)
        {
            dir.manufactureGO.BuildNewInstancesForLayer(ElementLayerKind.Fog);
        }

        // Then apply all color changes (including newly created ones)
        if (anyFogCreated || anyColorChanged)
        {
            dir.manufactureGO.ApplyPendingUpdates();
        }

        Debug.Log($"ScentFogUpdate completed. anyColorChanged={anyColorChanged}, anyFogCreated={anyFogCreated}");
    }

    /*
    void Assign_GO_Color(GameObject go, Color baseColor, float alpha = 1f)
    {
        // Color transparency based on scent amount
        MeshRenderer rend = go.GetComponent<MeshRenderer>(); // ok once per object, but avoid if not needed
        if (rend != null)
        {
            Color c = baseColor;
            c.a = alpha;
            rend.material.color = c;
        }
    }
    */
}

