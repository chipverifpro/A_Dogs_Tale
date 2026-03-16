// Assets/A_Dogs_Tale/Scripts/LLM/Core/LLMRequestPacketFormatter.cs
#nullable enable
using System.Text;
using DogGame.LLM.Prompting; // if your LLMJsonNormalizer lives here (adjust if not)

namespace DogGame.LLM.Core
{
    public static class LLMRequestPacketFormatter
    {
        /*
        public static string BuildPacketText(LLMRequest request)
        {
            var builder = new StringBuilder(4096);

            // SYSTEM BLOCKS
            if (request.systemBlocks != null && request.systemBlocks.Count > 0)
            {
                builder.AppendLine("SYSTEM BLOCKS:");
                for (int i = 0; i < request.systemBlocks.Count; i++)
                {
                    string block = request.systemBlocks[i] ?? "";
                    if (string.IsNullOrWhiteSpace(block)) continue;

                    builder.AppendLine(block.Trim());
                    builder.AppendLine();
                }
            }

            // USER PROMPT
            builder.AppendLine("USER PROMPT:");
            builder.AppendLine((request.userPrompt ?? "").Trim());
            builder.AppendLine();

            // TOOLS JSON (normalized for readability; still plain text)
            if (request.toolDefinitions!=null && !string.IsNullOrWhiteSpace(request!.toolDefinitions.ToString()))
            {
                builder.AppendLine("TOOL DEFINITIONS JSON:");
                builder.AppendLine(LLMJsonNormalizer.Normalize(request.toolDefinitions.ToString()));
                builder.AppendLine();
            }

            // RESPONSE SCHEMA JSON (normalized)
            if (!string.IsNullOrWhiteSpace(request.responseSchemaJson))
            {
                builder.AppendLine("RESPONSE SCHEMA JSON:");
                builder.AppendLine(LLMJsonNormalizer.Normalize(request.responseSchemaJson));
                builder.AppendLine();
            }

            return builder.ToString().Trim();
        }
        */
    }
}
