#nullable enable
using System;
using System.Reflection;
using DogGame.LLM;
using UnityEngine;

namespace DogGame.Tasks
{
    /// <summary>
    /// Adapter that bridges LLM-generated IAgentTask plans to your existing runner/scheduler.
    /// Uses reflection to call common method names so you don't have to couple code to a specific runner type.
    /// </summary>
    public sealed class AgentTaskRunnerAdapter : MonoBehaviour, IAgentTaskRunner
    {
        [Tooltip("Your existing scheduler/runner component that can execute IAgentTask graphs.")]
        [SerializeField] private MonoBehaviour? runnerComponent;

        [Header("Method name fallbacks (edit if your runner uses different names)")]
        [SerializeField] private string[] startMethodCandidates =
        {
            "StartTask",
            "Push",
            "Enqueue",
            "Queue",
            "Run",
            "Execute",
            "SetRootTask",
            "SetTask",
            "SetCurrentTask"
        };

        [SerializeField] private string[] abortMethodCandidates =
        {
            "AbortAll",
            "Abort",
            "CancelAll",
            "Cancel",
            "StopAll",
            "Stop",
            "Clear",
            "Reset"
        };

        public void StartTask(IAgentTask rootTask)
        {
            if (rootTask == null) return;

            if (runnerComponent == null)
            {
                Debug.LogWarning("[AgentTaskRunnerAdapter] runnerComponent is not assigned.", this);
                return;
            }

            if (!TryInvokeFirstMatch(runnerComponent, startMethodCandidates, rootTask))
            {
                Debug.LogWarning(
                    $"[AgentTaskRunnerAdapter] Could not find a compatible start method on runner '{runnerComponent.GetType().Name}'.\n" +
                    $"Tried: {string.Join(", ", startMethodCandidates)}\n" +
                    "Expected a method like StartTask(IAgentTask) or Push(IAgentTask).",
                    this);
                return;
            }

            Debug.Log($"[AgentTaskRunnerAdapter] Started: {rootTask.DebugName}", this);
        }

        public void AbortAll(string reason)
        {
            if (runnerComponent == null)
            {
                Debug.LogWarning("[AgentTaskRunnerAdapter] runnerComponent is not assigned.", this);
                return;
            }

            // First try abort methods that accept (string reason)
            if (TryInvokeFirstMatch(runnerComponent, abortMethodCandidates, reason))
            {
                Debug.Log($"[AgentTaskRunnerAdapter] AbortAll(reason): {reason}", this);
                return;
            }

            // Then try abort methods that accept no args
            if (TryInvokeFirstMatch(runnerComponent, abortMethodCandidates))
            {
                Debug.Log($"[AgentTaskRunnerAdapter] AbortAll(): {reason}", this);
                return;
            }

            Debug.LogWarning(
                $"[AgentTaskRunnerAdapter] Could not find a compatible abort method on runner '{runnerComponent.GetType().Name}'.\n" +
                $"Tried: {string.Join(", ", abortMethodCandidates)}\n" +
                "Expected AbortAll(string) / AbortAll() / Clear() / StopAll(), etc.",
                this);
        }

        private static bool TryInvokeFirstMatch(MonoBehaviour target, string[] methodNames, params object[] args)
        {
            var type = target.GetType();

            foreach (var methodName in methodNames)
            {
                // Look for exact name; accept public/nonpublic instance methods.
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (!string.Equals(m.Name, methodName, StringComparison.Ordinal))
                        continue;

                    var parameters = m.GetParameters();
                    if (!ArgsMatch(parameters, args))
                        continue;

                    try
                    {
                        m.Invoke(target, args);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AgentTaskRunnerAdapter] Invoke failed on {type.Name}.{m.Name}: {ex.Message}", target);
                        return false;
                    }
                }
            }

            return false;
        }

        private static bool ArgsMatch(ParameterInfo[] parameters, object[] args)
        {
            if (parameters.Length != args.Length)
                return false;

            for (int i = 0; i < parameters.Length; i++)
            {
                var pType = parameters[i].ParameterType;
                var arg = args[i];

                if (arg == null)
                {
                    // null can only go to reference types or nullable value types
                    if (pType.IsValueType && Nullable.GetUnderlyingType(pType) == null)
                        return false;
                    continue;
                }

                if (!pType.IsInstanceOfType(arg))
                {
                    // Allow a common case: parameter is object, arg is IAgentTask or string
                    if (pType == typeof(object))
                        continue;

                    return false;
                }
            }

            return true;
        }
    }
}