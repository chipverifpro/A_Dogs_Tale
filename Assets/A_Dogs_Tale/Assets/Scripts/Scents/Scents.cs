using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    // This routine runs forever, one iteration at most every cfg.ScentInterval seconds.
    public IEnumerator ScentDecayOnIntervals()
    {
        float decay_routine_start_time = Time.time;
        float time_since_previous = 0f;
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
        //int yields = 0;
        if ((time_since_previous / cfg.scentInterval) > 1.25f)
        {
            Debug.LogWarning($"ScentDecayAndSpread called after {time_since_previous} seconds, which is significantly longer than expected interval of {cfg.scentInterval} seconds.");
        }
        float scaled_spread_amount = cfg.scentSpreadAmount * (time_since_previous / cfg.scentInterval);
        float scaled_decay_rate = cfg.scentDecayRate * (time_since_previous / cfg.scentInterval);
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
                        float orig_intensity = c.scents[s].intensity;
                        spread_count = 0;
                        float spread_amount_per_direction = orig_intensity * scaled_spread_amount;
                        float accumulatedScentSpread = 0f;
                        // spread scent to nearby cells
                        foreach (DirFlags dirFlags in DirFlagsEx.AllCardinals)
                        {
                            //walls and closed doors block scent spread
                            if ((dirFlags & (c.walls | (c.doors))) == 0)    // TODO: include door open/closed
                            {
                                nPos = DirFlagsEx.ToVector2Int(dirFlags);
                                neighborCell = dir.gen.GetCellFromHf(c.x + nPos.x, c.y + nPos.y, c.z, threshold: 10);
                                if (neighborCell != null)
                                {
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

                        AddToNextScentIntensity(c, c.scents[s].agentId, orig_intensity
                                                                        - accumulatedScentSpread
                                                                        - decay_remaining_scent);
                    }
                }
            }

            // now, transfer all those NextScents back to Scents.
            foreach (Room room in dir.gen.rooms)
            {
                foreach (Cell cell in room.cells)
                {
                    if (cell.scents == null) cell.scents = new();

                    for (int scent_num = 0; scent_num < cell.scents.Count; scent_num++)
                    {
                        // only keep scent if above cfg.scentMinimum
                        if (cell.scents[scent_num].nextIntensity >= cfg.scentMinimum)
                        {
                            cell.scents[scent_num].intensity = cell.scents[scent_num].nextIntensity;
                            cell.scents[scent_num].nextIntensity = 0; // clear for next pass
                        }
                        else
                        {
                            cell.scents.RemoveAt(scent_num);
                            scent_num--;
                        }
                    }
                }
            }

            // yield every N rooms to allow other activities
            room_yield_counter--;
            if (room_yield_counter <= 0)
            {
                room_yield_counter = rooms_per_yield;
                //yields++;
                yield return null;
            }
        }
        //Debug.Log($"ScentDecayAndSpread completed after {yields} yields.");
    }

    public void AddToNextScentIntensity(Cell c, int agent_id, float? added_intensity = null, float? set_intensity = null)
    {
        if (c.scents == null) c.scents = new();
        //if (c.nextScents == null) c.nextScents = new();

        // if we find a matching agentId, add the scent amount
        for (int scent_num = 0; scent_num < c.scents.Count; scent_num++)
        {
            if (c.scents[scent_num].agentId == agent_id)
            {
                if (set_intensity.HasValue)
                    c.scents[scent_num].intensity = set_intensity.Value;    // overwrite current intensity
                if (added_intensity.HasValue)
                    c.scents[scent_num].nextIntensity += added_intensity.Value;

                return;   // found a match so we are done.
            }
        }

        // matching AgentId not found, add a new scent to list;
        ScentClass new_scent = new ScentClass
        {
            agentId = agent_id,
            nextIntensity = 0f,  //
            intensity = 0f       // will be overwritten in next transfer
        };
        if (set_intensity.HasValue)
            new_scent.intensity = set_intensity.Value;    // overwrite current intensity
        if (added_intensity.HasValue)
            new_scent.nextIntensity += added_intensity.Value;
        c.scents.Add(new_scent);
    }


    // This routine updates the scent fog visualization in each cell based on current scent amounts.
    // It will reuse existing fog GameObjects if they exist, or create new ones as needed.
    public void ScentFogUpdate()
    {
        GameObject scentFogPrefab = dir.gen.floorPrefab;    // temporary; replace with actual scent fog prefab later
        Color colorScent = new Color(0.5f, 1f, 0.5f, 0.5f); // light greenish translucent
        foreach (Room r in dir.gen.rooms)
        {
            foreach (Cell c in r.cells)
            {
                GameObject GO_fog = c.GOs[(int)Cell.GOtypes.Fog];    // grab existing fog object if it exists
                if (GO_fog == null)
                {
                    // create new fog object
                    GO_fog = Instantiate(scentFogPrefab, c.pos3d_f, Quaternion.Euler(90f, 0f, 0f), dir.gen.root);
                    GO_fog.transform.localScale = new Vector3(c.pos3d_f.x * 0.5f, c.pos3d_f.y * 0.5f, c.pos3d_f.z + 1f);
                    GO_fog.SetActive(false);            // hide until we need it
                    GO_fog.name = $"Fog({c.x},{c.y})";  // comment out in perf builds
                }
                // -------- Scent fog visualization --------
                if (GO_fog != null && c.scents != null)
                {
                    int scent_id = 1; // only visualize dummy scent_id 1 for now
                    for (int scent_num = 0; scent_num < c.scents.Count; scent_num++)
                    {
                        if (c.scents[scent_num].agentId == scent_id && c.scents[scent_num].intensity > 0f)
                        {
                            // found scent to visualize via transparency
                            float transparency = Mathf.Clamp01(c.scents[scent_num].intensity);
                            Assign_GO_Color(GO_fog, colorScent, transparency);
                            GO_fog.SetActive(true);
                        } else {
                            // no scent to visualize
                            GO_fog.SetActive(false);
                        }
                    }
                }
                c.GOs[(int)Cell.GOtypes.Fog] = GO_fog;   // save back to cell
            }
        }
    }

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
}

