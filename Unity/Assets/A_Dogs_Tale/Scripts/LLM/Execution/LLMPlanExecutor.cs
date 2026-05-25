#nullable enable
using DogGame.LLM.Translation;
using UnityEngine;

namespace DogGame.LLM.Execution
{
    /// <summary>
    /// Bridges validated LLM plans into the TaskSystem.
    /// This is the ONLY place that should know about:
    /// - PlanResponseV1Parser
    /// - Translators
    /// - Task factories / instantiation
    /// </summary>
    public sealed class LLMPlanExecutor
    {
        private readonly PlanResponseToTaskPlanTranslator translator;
        private readonly TaskPlanInstantiator instantiator;

        public LLMPlanExecutor(ITaskFactory taskFactory)
        {
            translator = new PlanResponseToTaskPlanTranslator();
            instantiator = new TaskPlanInstantiator(taskFactory);
        }

        /// <summary>
        /// Parse, validate, translate, and instantiate a plan.
        /// Returns a root task ready to be executed, or null on failure.
        /// </summary>
        public object? BuildRootTaskFromJson(string planJson)
        {
            // 1) Parse + validate
            var (response, validation) = PlanResponseV1Parser.ParseAndValidate(planJson);
            if (response == null)
            {
                Debug.LogWarning(
                    "[LLMPlanExecutor] Plan validation failed:\n" +
                    string.Join("\n", validation.Errors));
                return null;
            }

            // 2) Translate → TaskPlan
            TaskPlan taskPlan = translator.Translate(response);

            // 3) Instantiate → TaskSystem graph
            object rootTask = instantiator.InstantiateAsSequence(taskPlan);

            return rootTask;
        }
    }
}