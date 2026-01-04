#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class AgentTaskExecutor
    {
        private readonly AgentTaskQueue taskQueue;

        private TaskRequest? currentRequest;
        private IAgentTask? currentTask;
        private bool currentTaskStarted;

        // LIFO suspended task stack (pause/resume)
        private readonly Stack<TaskRequest> suspended = new();

        public string? CurrentTaskName => currentTask?.DebugName;
        public bool HasTask => currentTask != null;

        public int CurrentPriority => currentRequest?.Priority ?? -1;
        public int SuspendedCount => suspended.Count;

        public AgentTaskExecutor(AgentTaskQueue taskQueue)
        {
            this.taskQueue = taskQueue;
        }

        private int debugDoubleTick = -1;

        public void Tick(AgentTaskContext context, float deltaTimeSeconds)
        {
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: AgentTaskExecutor.Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            // Acquire next task if none is running (prefer suspended over queued)
            if (currentTask == null)
            {
                if (!TryAcquireNextRequest(out var next))
                    return;

                BeginRequest(next);
            }

            // Start once
            if (!currentTaskStarted)
            {
                currentTaskStarted = true;
                try
                {
                    currentTask!.Start(context);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"Task Start failed for {currentTask!.DebugName}: {exception.Message}");
                    EndCurrentTask(context, succeeded: false);
                    return;
                }
            }

            // Tick
            TaskTickResult tickResult;
            try
            {
                tickResult = currentTask!.Tick(context, deltaTimeSeconds);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Task Tick exception for {currentTask!.DebugName}: {exception.Message}");
                EndCurrentTask(context, succeeded: false);
                return;
            }

            if (tickResult.Status == TaskStatus.Running)
                return;

            if (tickResult.Status == TaskStatus.Failed)
                Debug.LogWarning($"Task failed: {currentTask!.DebugName} reason={tickResult.FailureReason}");

            EndCurrentTask(context, succeeded: tickResult.Status == TaskStatus.Succeeded);
        }

        /// <summary>
        /// Attempt to interrupt the currently running task with a new request.
        /// If successful, the new request becomes current immediately.
        /// </summary>
        public bool TryInterruptWith(AgentTaskContext context, TaskRequest incoming)
        {
            // If nothing is running, just start immediately.
            if (currentTask == null || currentRequest == null)
            {
                BeginRequest(incoming);
                return true;
            }

            // Must be allowed to interrupt and must outrank current.
            if (!incoming.CanInterrupt)
                return false;

            var running = currentRequest.Value;
            if (incoming.Priority <= running.Priority)
                return false;

            // Stack policy
            if (incoming.ClearStackOnStart)
                suspended.Clear();

            if (incoming.ResumePrevious)
            {
                // Suspend current then start incoming
                SuspendCurrent(context);
            }
            else
            {
                // End current immediately without saving it
                StopAndForgetCurrent(context);
            }

            BeginRequest(incoming);
            return true;
        }

        /// <summary>
        /// Push the current request onto the suspended stack and stop it (so it can be resumed later).
        /// </summary>
        public bool SuspendCurrent(AgentTaskContext context)
        {
            if (currentRequest == null || currentTask == null)
                return false;

            SafeStop(context);

            suspended.Push(currentRequest.Value);

            currentRequest = null;
            currentTask = null;
            currentTaskStarted = false;

            // Clear movement intent so we don't drift while suspended.
            context.Movement.StopMoving();
            return true;
        }

        /// <summary>
        /// Resume the most recently suspended task immediately (if no task is running).
        /// </summary>
        public bool ResumeSuspended()
        {
            if (currentTask != null)
                return false;

            if (suspended.Count == 0)
                return false;

            BeginRequest(suspended.Pop());
            return true;
        }

        /// <summary>
        /// Clears all queued and suspended tasks and stops the current task.
        /// Useful for player takeover / panic / reset.
        /// </summary>
        public void ClearAll(AgentTaskContext context)
        {
            taskQueue.Clear();
            suspended.Clear();
            StopAndForgetCurrent(context);
        }

        private bool TryAcquireNextRequest(out TaskRequest next)
        {
            if (suspended.Count > 0)
            {
                next = suspended.Pop();
                return true;
            }

            return taskQueue.TryDequeue(out next);
        }

        private void BeginRequest(TaskRequest request)
        {
            currentRequest = request;
            currentTask = request.Task;
            currentTaskStarted = false;
        }

        private void EndCurrentTask(AgentTaskContext context, bool succeeded)
        {
            SafeStop(context);

            // Ensure agent doesn't keep moving when tasks end.
            context.Movement.StopMoving();

            currentRequest = null;
            currentTask = null;
            currentTaskStarted = false;

            // Auto-resume happens naturally next Tick because TryAcquireNextRequest()
            // prefers suspended tasks over queued tasks.
        }

        private void StopAndForgetCurrent(AgentTaskContext context)
        {
            if (currentTask == null)
                return;

            SafeStop(context);
            context.Movement.StopMoving();

            currentRequest = null;
            currentTask = null;
            currentTaskStarted = false;
        }

        private void SafeStop(AgentTaskContext context)
        {
            try
            {
                currentTask?.Stop(context);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Task Stop exception: {exception.Message}");
            }
        }
    }
}