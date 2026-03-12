#nullable enable
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
        private readonly PerceptionEvent perceptionEvent;

        public DogLuaBindings(
            TaskController taskController,
            WorldObject observer,
            PerceptionEvent perceptionEvent)
        {
            this.taskController = taskController;
            this.observer = observer;
            this.perceptionEvent = perceptionEvent;
        }

        public void Bark(int times)
        {
            int barkCount = Mathf.Max(1, times);

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
                taskSpec: TS.MoveToEvent(stopRadius),
                priority: 55,
                tag: $"Lua:MoveToEvent:{stopRadius:0.##}");
        }

        public void MoveToTarget(float stopRadius)
        {
            Enqueue(
                taskSpec: TS.MoveToTarget(stopRadius),
                priority: 55,
                tag: $"Lua:MoveToTarget:{stopRadius:0.##}");
        }

        private void Enqueue(TaskSpec taskSpec, int priority, string tag)
        {
            if (taskController == null)
            {
                Debug.LogError("[DogLuaBindings] taskController is null.");
                return;
            }

            if (observer == null)
            {
                Debug.LogError("[DogLuaBindings] observer is null.");
                return;
            }

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

            taskController.EnqueueTask(
                task: builtTask,
                priority: priority,
                source: TaskSource.Lua,
                canInterrupt: true,
                resumePrevious: false,
                clearStackOnStart: false,
                tag: tag,
                front: false);
        }
    }
}