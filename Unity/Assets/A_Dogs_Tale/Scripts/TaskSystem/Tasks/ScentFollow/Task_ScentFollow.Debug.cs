#nullable enable
using System.Text;
using UnityEngine;

namespace DogGame.Tasks
{
    public sealed partial class Task_ScentFollow
    {
        private void DebugPrintScentMemory()
        {
            if (memory.Count == 0)
            {
                Debug.Log("[ScentFollow] Memory is empty");
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine($"[ScentFollow] Memory ({memory.Count} cells):");

            foreach (var kvp in memory)
            {
                Vector2Int pos = kvp.Key;
                ScentMemory cell = kvp.Value;

                sb.AppendLine(
                    $"  {pos.x,3},{pos.y,3} | " +
                    $"air={cell.airScent:0.000} " +
                    $"ground={cell.groundScent:0.000} " +
                    $"combined={cell.combinedScent:0.000} " +
                    $"peak={cell.peakCombinedScent:0.000} " +
                    $"sniffed={cell.sniffed} " +
                    $"visited={cell.visited} " +
                    $"visits={cell.visitCount} " +
                    $"lastSeen={Time.time - cell.timeChecked:0.0}s ago"
                );
            }

            Debug.Log(sb.ToString());
        }
    }
}
