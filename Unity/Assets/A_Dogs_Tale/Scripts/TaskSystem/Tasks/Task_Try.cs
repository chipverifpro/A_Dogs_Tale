#nullable enable
using DogGame.LLM;
using UnityEngine;

namespace DogGame.Tasks
{
    /// <summary>
    /// Try a task. If it fails, optionally run a fallback task.
    /// Optionally run an onSuccess task after success.
    ///
    /// Default behavior:
    /// - If tryTask succeeds: succeed (after optional onSuccess).
    /// - If tryTask fails:
    ///     - If onFail exists: run it; if it succeeds, THIS TASK succeeds (failure handled).
    ///     - If onFail is null: propagate failure.
    /// </summary>
    public sealed class Task_Try : IAgentTask
    {
        private enum Phase
        {
            Try,
            OnSuccess,
            OnFail,
            Done
        }

        private readonly IAgentTask tryTask;
        private readonly IAgentTask? onSuccess;
        private readonly IAgentTask? onFail;

        private Phase phase;
        private IAgentTask? current;
        private bool currentStarted;

        public string DebugName
        {
            get
            {
                string p = phase.ToString();
                string c = current?.DebugName ?? "null";
                return $"Try({p}): {c}";
            }
        }

        public Task_Try(IAgentTask tryTask, IAgentTask? onFail = null, IAgentTask? onSuccess = null)
        {
            this.tryTask = tryTask;
            this.onFail = onFail;
            this.onSuccess = onSuccess;
        }

        public void Start(TaskContext context)
        {
            phase = Phase.Try;
            current = tryTask;
            currentStarted = false;
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (phase == Phase.Done || current == null)
                return TaskTickResult.Succeeded();

            // Start current once
            if (!currentStarted)
            {
                currentStarted = true;
                try
                {
                    current.Start(context);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Task_Try current Start failed: {ex.Message}");
                    SafeStopCurrent(context);
                    return HandleCurrentFailure(context, "try_start_failed");
                }
            }

            TaskTickResult result;
            try
            {
                result = current.Tick(context, deltaTimeSeconds);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Task_Try current Tick exception: {ex.Message}");
                SafeStopCurrent(context);
                return HandleCurrentFailure(context, "try_tick_exception");
            }

            if (result.Status == TaskStatus.Running)
                return result;

            // Finished -> stop it
            SafeStopCurrent(context);

            if (result.Status == TaskStatus.Succeeded)
            {
                // Try succeeded
                if (phase == Phase.Try && onSuccess != null)
                {
                    phase = Phase.OnSuccess;
                    current = onSuccess;
                    currentStarted = false;
                    return TaskTickResult.Running();
                }

                // onSuccess finished (or none)
                phase = Phase.Done;
                current = null;
                return TaskTickResult.Succeeded();
            }

            // Failed
            return HandleCurrentFailure(context, result.FailureReason ?? "try_failed");
        }

        public void Stop(TaskContext context)
        {
            SafeStopCurrent(context);
            phase = Phase.Done;
            current = null;
            currentStarted = false;
        }

        private TaskTickResult HandleCurrentFailure(TaskContext context, string reason)
        {
            if (phase == Phase.Try)
            {
                // If we have fallback, run it and treat failure as "handled" if fallback succeeds.
                if (onFail != null)
                {
                    phase = Phase.OnFail;
                    current = onFail;
                    currentStarted = false;
                    return TaskTickResult.Running();
                }

                // No fallback -> propagate failure
                phase = Phase.Done;
                current = null;
                return TaskTickResult.Failed(reason);
            }

            if (phase == Phase.OnFail)
            {
                // If fallback fails, propagate fallback failure.
                phase = Phase.Done;
                current = null;
                return TaskTickResult.Failed(reason);
            }

            // onSuccess failing is usually treated as a failure
            phase = Phase.Done;
            current = null;
            return TaskTickResult.Failed(reason);
        }

        private void SafeStopCurrent(TaskContext context)
        {
            try
            {
                current?.Stop(context);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Task_Try current Stop exception: {ex.Message}");
            }
        }
    }
}