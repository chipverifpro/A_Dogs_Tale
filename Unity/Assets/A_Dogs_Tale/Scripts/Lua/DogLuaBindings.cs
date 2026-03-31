#nullable enable
using System;
using UnityEngine;
using DogGame.Reactions;
using DogGame.Tasks;
using DogGame.LLM;
using DogGame.Modules;

namespace DogGame.Lua
{
    public class DogLuaBindings
    {
        private readonly TaskController taskController;
        private readonly WorldObject observer;
        private PerceptionEvent perceptionEvent;

        public DogLuaBindings(
            TaskController taskController,
            WorldObject observer,
            PerceptionEvent perceptionEvent)
        {
            this.taskController = taskController;
            this.observer = observer;
            this.perceptionEvent = perceptionEvent;
        }

        public void SetPerceptionEvent(PerceptionEvent perceptionEvent)
        {
            this.perceptionEvent = perceptionEvent;
        }

        public void Bark(int times)
        {
            int barkCount = Mathf.Clamp(times, 1, 5);

            for (int barkIndex = 0; barkIndex < barkCount; barkIndex++)
            {
                Enqueue(
                    taskSpec: TS.Bark(volume10: 6),
                    priority: 60,
                    tag: "Lua:Bark");
            }
        }

        public void MoveToEvent(float stopRadius)
        {
            Enqueue(
                taskSpec: TS.MoveToEvent(),
                priority: 55,
                tag: "Lua:MoveToEvent");
        }

        public void MoveToTarget(float stopRadius)
        {
            Enqueue(
                taskSpec: TS.MoveToTarget(),
                priority: 55,
                tag: "Lua:MoveToTarget");
        }

        public void FaceEventTarget(float toleranceDeg, float maxSeconds)
        {
            Enqueue(
                taskSpec: TS.FaceTarget(toleranceDeg, maxSeconds),
                priority: 56,
                tag: $"Lua:FaceEventTarget:{toleranceDeg:0.##}:{maxSeconds:0.##}");
        }

        public void MoveUntilEventSeen(float stopRadius, float maxSeconds)
        {
            Enqueue(
                taskSpec: TS.MoveUntilSeen(
                    maxSeconds: maxSeconds),
                priority: 56,
                tag: $"Lua:MoveUntilEventSeen:{maxSeconds:0.##}");
        }

        public void MoveToEventSound(float stopRadius)
        {
            if (!perceptionEvent.Sound.HasValue)
            {
                Debug.LogError("[DogLuaBindings] MoveToEventSound called but current event has no sound payload.");
                return;
            }

            Enqueue(
                taskSpec: TS.MoveToEvent(),
                priority: 56,
                tag: "Lua:MoveToEventSound");
        }

        public void Sniff(float seconds)
        {
            float clampedSeconds = Mathf.Clamp(seconds, 0.05f, 10f);

            Enqueue(
                taskSpec: TS.Sniff(clampedSeconds),
                priority: 56,
                tag: $"Lua:Sniff:{clampedSeconds:0.##}");
        }

        public void FollowScent(string scentKey, string medium)
        {
            if (string.IsNullOrWhiteSpace(scentKey))
            {
                Debug.LogError("[DogLuaBindings] FollowScent requires a non-empty scentKey.");
                return;
            }

            ScentMedium scentMedium = ParseScentMedium(medium);

            EnqueueTask(
                task: new Task_ScentFollow(scentKey.Trim(), scentMedium),
                priority: 58,
                tag: $"Lua:FollowScent:{scentMedium}:{scentKey.Trim()}");
        }

        public void FollowEventScent()
        {
            FollowEventScentInternal(ScentMedium.Ground);
        }

        public void FollowEventScentAir()
        {
            FollowEventScentInternal(ScentMedium.Air);
        }

        private void FollowEventScentInternal(ScentMedium medium)
        {
            if (!perceptionEvent.Scent.HasValue)
            {
                Debug.LogError("[DogLuaBindings] FollowEventScent called but current event has no scent payload.");
                return;
            }

            string scentKey = perceptionEvent.Scent.Value.ScentKey;
            if (string.IsNullOrWhiteSpace(scentKey))
            {
                Debug.LogError("[DogLuaBindings] FollowEventScent could not resolve scent key from event.");
                return;
            }

            EnqueueTask(
                task: new Task_ScentFollow(scentKey, medium),
                priority: 58,
                tag: $"Lua:FollowEventScent:{medium}:{scentKey}");
        }

        private static ScentMedium ParseScentMedium(string medium)
        {
            if (string.Equals(medium, "air", StringComparison.OrdinalIgnoreCase))
                return ScentMedium.Air;

            return ScentMedium.Ground;
        }

        private void Enqueue(TaskSpec taskSpec, int priority, string tag)
        {
            if (!CanEnqueue())
                return;

            if (!TaskSpecFactory.TryBuildTask(
                    spec: taskSpec,
                    observer: observer,
                    e: perceptionEvent,
                    task: out IAgentTask? builtTask,
                    error: out string? error))
            {
                Debug.LogError($"[DogLuaBindings] Failed to build task for '{taskSpec.Name}': {error}");
                return;
            }

            if (builtTask == null)
            {
                Debug.LogError($"[DogLuaBindings] TryBuildTask succeeded but returned null for '{taskSpec.Name}'.");
                return;
            }

            EnqueueTask(builtTask, priority, tag);
        }

        private void EnqueueTask(IAgentTask task, int priority, string tag)
        {
            if (!CanEnqueue())
                return;

            taskController.EnqueueTask(
                task: task,
                priority: priority,
                source: TaskSource.Lua,
                canInterrupt: true,
                resumePrevious: false,
                clearStackOnStart: false,
                tag: tag,
                front: false);
        }

        private bool CanEnqueue()
        {
            if (taskController == null)
            {
                Debug.LogError("[DogLuaBindings] taskController is null.");
                return false;
            }

            if (observer == null)
            {
                Debug.LogError("[DogLuaBindings] observer is null.");
                return false;
            }

            return true;
        }
    }
}
