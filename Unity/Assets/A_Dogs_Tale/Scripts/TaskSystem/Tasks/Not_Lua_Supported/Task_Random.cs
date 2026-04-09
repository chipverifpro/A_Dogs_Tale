#nullable enable
using System;
using DogGame.LLM;
using UnityEngine;

namespace DogGame.Tasks
{
    /// <summary>
    /// Chooses one subtask at random and executes it.
    /// The choice is made once, on first Tick.
    /// </summary>
    public sealed class Task_Random : IAgentTask
    {
        public string DebugName { get; }
        public string Description = "Chooses one subtask at random on first tick, runs that task to completion, and forwards its result.";

        private readonly IAgentTask[] options;
        private IAgentTask? chosen;
        private bool started;

        public Task_Random(
            IAgentTask[] options,
            string debugName = "Random")
        {
            if (options == null || options.Length == 0)
                throw new ArgumentException("Task_Random requires at least one option.", nameof(options));

            this.options = options;
            DebugName = debugName;
        }

        public void Start(TaskContext context)
        {
            // No-op. We select on first Tick to keep semantics consistent with other tasks.
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (!started)
            {
                started = true;

                int index = UnityEngine.Random.Range(0, options.Length);
                chosen = options[index];

                if (chosen == null)
                {
                    Debug.LogWarning($"[{DebugName}] Chosen task was null.");
                    return TaskTickResult.Failed("Random task chose null option");
                }

                try
                {
                    chosen.Start(context);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[{DebugName}] Start failed: {exception.Message}");
                    return TaskTickResult.Failed("Random subtask start exception");
                }
            }

            if (chosen == null)
                return TaskTickResult.Failed("Random task has no chosen subtask");

            try
            {
                return chosen.Tick(context, deltaTimeSeconds);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{DebugName}] Tick failed: {exception.Message}");
                return TaskTickResult.Failed("Random subtask tick exception");
            }
        }

        public void Stop(TaskContext context)
        {
            if (chosen == null)
                return;

            try
            {
                chosen.Stop(context);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{DebugName}] Stop failed: {exception.Message}");
            }
        }
    }
}

/* Example usages:

new Task_Random(new IAgentTask[]
{
    new Task_Wait(0.5f),
    new Task_Bark(3),
    new Task_Emote("tilt_head")
});

new Task_Random(new IAgentTask[]
{
    new Task_MoveManual(new Vector2(1, 0), 0.5f),
    new Task_MoveManual(new Vector2(-1, 0), 0.5f),
    new Task_MoveManual(new Vector2(0, 1), 0.5f),
    new Task_MoveManual(new Vector2(0, -1), 0.5f)
}, debugName: "RandomShuffle");

new Task_Sequence(new IAgentTask[]
{
    new Task_Bark(2),
    new Task_Random(new IAgentTask[]
    {
        new Task_Sniff(1.0f),
        new Task_Wait(0.3f),
        new Task_Emote("scratch")
    }),
    new Task_MoveToCell(5, 6, 0.3f)
});

new Task_Repeat(
    times: 5,
    task: new Task_Random(new IAgentTask[]
    {
        new Task_Sniff(0.5f),
        new Task_MoveManual(Random.insideUnitCircle, 0.4f),
        new Task_Wait(0.2f)
    })
);

*/
