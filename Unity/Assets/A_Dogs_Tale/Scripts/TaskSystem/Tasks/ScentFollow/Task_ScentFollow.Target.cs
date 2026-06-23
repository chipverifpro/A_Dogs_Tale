#nullable enable
using System;
using DogGame.LLM;
using UnityEngine;

namespace DogGame.Tasks
{
    public sealed partial class Task_ScentFollow
    {
        private readonly float foundStrengthThreshold01 = 0.20f;
        private readonly float plateauEpsilon = 0.005f;
        private float lastHeuristicStrength01;
        private Vector2Int lastHeuristicPos;
        private bool hasLastHeuristicSample;
        private int plateauTicks;

        private bool IsTargetReached(TaskContext context)
        {
            if (context.Agent == null)
                return false;

            if (!TryParseAgentId(scentKey, out int targetId))
                return false;

            if (!WorldObjectRegistry.Instance.TryGet(targetId, out var target) || target == null)
                return false;

            Vector2Int agentPos = context.Agent.locationModule.cell.pos;
            Vector2Int targetPos = target.locationModule.cell.pos;

            int manhattan = Mathf.Abs(agentPos.x - targetPos.x) + Mathf.Abs(agentPos.y - targetPos.y);
            return manhattan <= 1;
        }

        private static bool TryParseAgentId(string key, out int id)
        {
            id = -1;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (!key.StartsWith("agent:", StringComparison.Ordinal))
                return false;

            return int.TryParse(key.Substring("agent:".Length), out id);
        }

        private bool IsHeuristicFound(Vector2Int pos)
        {
            float current = GetLastStrength(pos);
            if (current < foundStrengthThreshold01)
            {
                plateauTicks = 0;
                hasLastHeuristicSample = false;
                return false;
            }

            if (hasLastHeuristicSample && pos == lastHeuristicPos)
            {
                if (current <= lastHeuristicStrength01 + plateauEpsilon)
                    plateauTicks++;
                else
                    plateauTicks = 0;
            }
            else
            {
                plateauTicks = 0;
                hasLastHeuristicSample = true;
            }

            lastHeuristicPos = pos;
            lastHeuristicStrength01 = current;

            return plateauTicks >= 3;
        }
    }
}
