#nullable enable
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class AgentTaskExecutor
    {
        private readonly AgentTaskQueue taskQueue;
        private IAgentTask? currentTask;
        private bool currentTaskStarted;

        public string? CurrentTaskName => currentTask?.DebugName;
        public bool HasTask => currentTask != null;

        public AgentTaskExecutor(AgentTaskQueue taskQueue)
        {
            this.taskQueue = taskQueue;
        }

        public void Tick(AgentTaskContext context, float deltaTimeSeconds)
        {
            // If no current task, pull next.
            if (currentTask == null)
            {
                if (!taskQueue.TryDequeue(out currentTask))
                    return;

                currentTaskStarted = false;
            }

            // Start task once.
            if (!currentTaskStarted)
            {
                currentTaskStarted = true;
                try
                {
                    currentTask.Start(context);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"Task Start failed for {currentTask.DebugName}: {exception.Message}");
                    EndCurrentTask(context, succeeded: false);
                    return;
                }
            }

            // Tick task.
            TaskTickResult tickResult;
            try
            {
                tickResult = currentTask.Tick(context, deltaTimeSeconds);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Task Tick exception for {currentTask.DebugName}: {exception.Message}");
                EndCurrentTask(context, succeeded: false);
                return;
            }

            if (tickResult.Status == TaskStatus.Running)
                return;

            if (tickResult.Status == TaskStatus.Failed)
                Debug.LogWarning($"Task failed: {currentTask.DebugName} reason={tickResult.FailureReason}");

            EndCurrentTask(context, succeeded: tickResult.Status == TaskStatus.Succeeded);
        }

        private void EndCurrentTask(AgentTaskContext context, bool succeeded)
        {
            try
            {
                currentTask?.Stop(context);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Task Stop exception: {exception.Message}");
            }

            // Ensure agent doesn't keep moving if a move task ends.
            context.Movement.StopMoving();

            currentTask = null;
            currentTaskStarted = false;
        }
    }
}