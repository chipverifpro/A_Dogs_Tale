#nullable enable
using System.Collections.Generic;
using DogGame.LLM;
using UnityEngine;

namespace DogGame.Tasks
{
    /// <summary>
    /// Runs a list of tasks in order as a single task.
    /// From the scheduler's point of view, this is ONE task (one priority).
    /// </summary>
    public sealed class Task_Sequence : IAgentTask, ICompositeAgentTask
    {
        private readonly List<IAgentTask> steps;
        private int stepIndex;
        private IAgentTask? currentStep;
        private bool currentStepStarted;

        public string DebugName
        {
            get
            {
                if (steps.Count == 0) return "Sequence(empty)";
                if (currentStep == null) return $"Sequence(done {steps.Count} steps)";
                return $"Sequence[{stepIndex + 1}/{steps.Count}]: {currentStep.DebugName}";
            }
        }
        public string Description = "Runs a list of child tasks in order as a single composite task and fails immediately if any step fails.";

        public Task_Sequence(IEnumerable<IAgentTask> tasks)
        {
            steps = new List<IAgentTask>(tasks);
            stepIndex = 0;
        }

        // parameterless constructor
        public Task_Sequence()
        {
            steps = new List<IAgentTask>();
            stepIndex = 0;
        }

        public void AddChild(IAgentTask child)
        {
            if (child == null)
                throw new System.ArgumentNullException(nameof(child));

            steps.Add(child);
        }

        public void Start(TaskContext context)
        {
            stepIndex = 0;
            currentStep = null;
            currentStepStarted = false;

            if (steps.Count > 0)
                currentStep = steps[0];
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (steps.Count == 0)
                return TaskTickResult.Succeeded();

            if (currentStep == null)
                return TaskTickResult.Succeeded();

            // Start current step once
            if (!currentStepStarted)
            {
                currentStepStarted = true;
                try
                {
                    currentStep.Start(context);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Sequence step Start failed: {ex.Message}");
                    SafeStopCurrent(context);
                    return TaskTickResult.Failed("sequence_step_start_failed");
                }
            }

            TaskTickResult stepResult;
            try
            {
                stepResult = currentStep.Tick(context, deltaTimeSeconds);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Sequence step Tick exception: {ex.Message}");
                SafeStopCurrent(context);
                return TaskTickResult.Failed("sequence_step_tick_exception");
            }

            if (stepResult.Status == TaskStatus.Running)
                return stepResult;

            // Step finished (success or fail) -> stop it
            SafeStopCurrent(context);

            // Fail-fast: if any step fails, the whole sequence fails.
            if (stepResult.Status == TaskStatus.Failed)
                return stepResult;

            // Advance to next step
            stepIndex++;
            if (stepIndex >= steps.Count)
            {
                currentStep = null;
                return TaskTickResult.Succeeded();
            }

            currentStep = steps[stepIndex];
            currentStepStarted = false;

            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            SafeStopCurrent(context);
            currentStep = null;
            currentStepStarted = false;
        }

        private void SafeStopCurrent(TaskContext context)
        {
            try
            {
                currentStep?.Stop(context);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Sequence step Stop exception: {ex.Message}");
            }
        }
    }
}
