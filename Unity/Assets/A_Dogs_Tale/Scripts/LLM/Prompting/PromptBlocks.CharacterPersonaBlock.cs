using System.Text;
using DogGame.LLM.Agent;

namespace DogGame.LLM.Prompting
{
    public static partial class PromptBlocks
    {
        public static string CharacterPersonaBlock(CharacterBuild c)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("CHARACTER:");
            if (!string.IsNullOrWhiteSpace(c.archetype))
                sb.AppendLine($"- Archetype: {c.archetype}");
            if (!string.IsNullOrWhiteSpace(c.background))
                sb.AppendLine($"- Background: {c.background}");

            if (c.goals != null && c.goals.Count > 0)
            {
                sb.AppendLine("- Goals:");
                for (int i = 0; i < c.goals.Count; i++)
                    sb.AppendLine($"  - {c.goals[i]}");
            }

            if (c.quirks != null && c.quirks.Count > 0)
            {
                sb.AppendLine("- Quirks:");
                for (int i = 0; i < c.quirks.Count; i++)
                    sb.AppendLine($"  - {c.quirks[i]}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}