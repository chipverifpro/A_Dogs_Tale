using System.Collections;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{
    public void Start_Scents()
    {
        StartCoroutine(ScentDecayOnIntervals());
    }

    // This routine runs forever, one iteration at most every cfg.ScentInterval seconds.
    public IEnumerator ScentDecayOnIntervals()
    {
        float decay_routine_start_time = Time.time;
        float time_since_previous = 0f;
        while (true)
        {
            if (time_since_previous < cfg.ScentInterval) {
                yield return new WaitForSeconds(cfg.ScentInterval - time_since_previous); 
                time_since_previous = Time.time - decay_routine_start_time;
            }
            
            decay_routine_start_time = Time.time;
            yield return StartCoroutine(ScentDecayAndSpread(time_since_previous));
            time_since_previous = Time.time - decay_routine_start_time;
        }
    }

    // This routine is called approximately every cfg.ScentInterval (maybe longer if previous iteration took long)
    //   It scans every Cell in every Room looking for existing scents.
    //   It decays scent by cfg.ScentDecayRate.
    //   It spreads scent to each neighbor by cfg.ScentSpradAmount.
    //   All of these are put into a nextScent list so we only work on the originals.
    //     It moves all the nextScent list entries to the scent list except...
    //       It removes the scent if it decays below cfg.ScentMinimum
    public IEnumerator ScentDecayAndSpread(float time_since_previous)
    {
        Cell neighborCell;
        Vector2Int nPos;
        int spread_count;
        int rooms_per_yield = 10;   // adjust as needed
        int room_yield_counter = rooms_per_yield;
        if (time_since_previous - cfg.ScentInterval > 0.1f)
        {
            Debug.LogWarning($"ScentDecayAndSpread called after {time_since_previous} seconds, which is longer than expected interval of {cfg.ScentInterval} seconds.");
        }
        float scaled_spread_amount = cfg.ScentSpreadAmount * (time_since_previous / cfg.ScentInterval);
        float scaled_decay_rate =    cfg.ScentDecayRate    * (time_since_previous / cfg.ScentInterval);
        // Check for problems at long intervals
        if (scaled_spread_amount > 1f/10f)
        {
            Debug.LogError($"ScentDecayAndSpread: scaled_spread_amount {scaled_spread_amount} is too high and will likely cause complete collapse of scent algorithm.");
            scaled_spread_amount = 1f/10f; // clamp it, but this only masks the real problem.
            // result at 1/4th is that original scent disappears in one iteration.
            // result at 1/5th is that scent in adjacent cells becomes greater than original in one iteration, making tracking impossible.
            // is 1/6th safe?  need to analyze more.  I'd feel more comfortable at 1/10th.
        }
        yield return null;
        if (!buildComplete) yield break;    // can't do scents until build is done.

        foreach (Room r in rooms)
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
                        foreach (DirFlags dir in DirFlagsEx.AllCardinals)
                        {
                            //walls and closed doors block scent spread
                            if ((dir & (c.walls | (c.doors /* & c.doors_closed*/))) == 0)    // TODO: include door open/closed
                            {
                                nPos = DirFlagsEx.ToVector2Int(dir);
                                neighborCell = GetCellFromHf(c.x + nPos.x, c.y + nPos.y, c.z, threshold: 10);
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
                // yield every N rooms to allow other activities
                rooms_per_yield--;
                if (rooms_per_yield <= 0)
                {
                    rooms_per_yield = room_yield_counter;
                    yield return null;
                }
            }

            // now, transfer all those NextScents back to Scents.
            foreach (Room room in rooms)
            {
                foreach (Cell cell in room.cells)
                {
                    if (cell.scents == null) cell.scents = new();

                    for (int scent_num = 0; scent_num < cell.scents.Count; scent_num++)
                    {
                        // only keep scent if above cfg.ScentMinimum
                        if (cell.scents[scent_num].nextIntensity >= cfg.ScentMinimum)
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
                // yield every N rooms to allow other activities
                rooms_per_yield--;
                if (rooms_per_yield <= 0)
                {
                    rooms_per_yield = room_yield_counter;
                    yield return null;
                }
            }
        }
    }

    void AddToNextScentIntensity(Cell c, int agent_id, float added_intensity)
    {
       if (c.scents == null) c.scents = new();
       if (c.nextScents == null) c.nextScents = new();

        // if we find a matching agentId, add the scent amount
        for (int scent_num = 0; scent_num < c.scents.Count; scent_num++)
        {
            if (c.scents[scent_num].agentId == agent_id)
            {
                c.scents[scent_num].nextIntensity += added_intensity; //orig_scent.intensity * cfg.ScentSpreadAmount;
                return;   // found a match so we are done.
            }
        }

        // matching AgentId not found, add a new scent to list;
        ScentClass new_scent = new ScentClass
        {
            agentId = agent_id,
            nextIntensity = added_intensity
        };
        c.scents.Add(new_scent);
    }


}

