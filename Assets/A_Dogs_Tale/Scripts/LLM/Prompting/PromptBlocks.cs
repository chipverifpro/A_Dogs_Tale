namespace DogGame.LLM.Prompting
{
    public static class PromptBlocks
    {
        public static string GlobalRulesBlock()
        {
            return
@"GLOBAL RULES:
- Stay in-character based on the persona.
- Be concise unless the situation is complex.
- If tools are enabled, only call allowed tools.
- If unsure, ask ONE clarifying question or make the safest assumption and proceed.";
        }

        public static string PlanningGuidanceBlock(int planningDepth)
        {
            // Important: we are NOT requesting chain-of-thought. This is just behavioral guidance.
            return planningDepth switch
            {
                0 => "PLANNING: Act reactively. Prefer immediate, simple actions.",
                1 => "PLANNING: Think one step ahead. Prefer safe, robust actions.",
                _ => "PLANNING: Consider 2-3 steps ahead and contingencies. Prefer high-quality decisions."
            };
        }

        public static string ContextHeader(string title)
        {
            return $"CONTEXT: {title}";
        }
    }
}