#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DogGame.LLM.Agent
{
    [Serializable]
    public sealed class InstructionSection
    {
        [Header("System Blocks (optional extra rules)")]
        [Tooltip("Extra system blocks to append after GlobalRules/PlanningGuidance/Personality.")]
        [TextArea(2, 12)]
        public List<string> extraSystemBlocks = new();

        [Header("Tools / Schema Sources")]
        [Tooltip("If assigned, we will serialize this as the tool catalog JSON.")]
        public TextAsset? toolCatalogJson;

        [Tooltip("If assigned, we will serialize this as the response schema JSON.")]
        public TextAsset? responseSchemaJson;

        [Tooltip("If true, embed tool catalog + schema as JSON objects in the request packet (preferred). If false, embed as raw strings.")]
        public bool embedAsStructuredJson = true;

        [SerializeField] private string toolCatalogResourcePath = "LLM/Tools/ToolCatalogV3"; // no extension
        [SerializeField] private string responseSchemaResourcePath = "LLM/Schemas/PlanResponseV3.schema"; // no extension

        public void AddSystemBlocks(List<string> systemBlocks)
        {
            if (systemBlocks == null) throw new ArgumentNullException(nameof(systemBlocks));
            if (extraSystemBlocks == null || extraSystemBlocks.Count == 0) return;

            for (int i = 0; i < extraSystemBlocks.Count; i++)
            {
                var block = extraSystemBlocks[i];
                if (string.IsNullOrWhiteSpace(block)) continue;
                systemBlocks.Add(block.Trim());
            }
        }

        /// <summary>
        /// Returns the JSON string describing available tools/tasks the LLM may reference.
        /// Source: TextAsset if provided, otherwise a minimal fallback.
        /// </summary>
        /*
        public JObject OLD_BuildToolDefinitionsJson()
        {
            var fallback = new JObject
            {
                ["schema"] = "ToolCatalogV1",
                ["tools"] = new JArray()
            };
            return fallback;
        }
        */

        private JObject? cachedToolCatalog;
        private JObject? cachedResponseSchema;

        public JObject BuildToolDefinitionsJson()
        {
            if (cachedToolCatalog != null)
                return (JObject)cachedToolCatalog.DeepClone();

            string resolvedToolCatalogResourcePath = ResolveToolCatalogResourcePath();
            if (toolCatalogJson == null || string.Equals(toolCatalogJson.name, "ToolCatalogV1", StringComparison.Ordinal))
                toolCatalogJson = Resources.Load<TextAsset>(resolvedToolCatalogResourcePath) ?? toolCatalogJson;

            if (toolCatalogJson == null)
            {
                Debug.LogError($"Tool catalog JSON not found at Resources/{resolvedToolCatalogResourcePath}.json");

                return new JObject
                {
                    ["schema"] = "ToolCatalogV3",
                    ["tools"] = new JArray()
                };
            }

            try
            {
                var parsed = JObject.Parse(toolCatalogJson.text);

                var result = new JObject
                {
                    ["schema"] = parsed.Value<string>("schema") ?? "ToolCatalogV3",
                    ["description"] = parsed.Value<string>("description") ?? "",
                    ["criticalRules"] = parsed["criticalRules"]?.DeepClone(),
                    ["notes"] = parsed["notes"]?.DeepClone(),
                    ["taskParameterConvention"] = parsed["taskParameterConvention"]?.DeepClone(),
                    ["tools"] = parsed["tools"]?.DeepClone() ?? parsed["tasks"]?.DeepClone() ?? new JArray()
                };

                cachedToolCatalog = result;
                return (JObject)cachedToolCatalog.DeepClone();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Tool catalog parse failed: {ex.Message}");

                return new JObject
                {
                    ["schema"] = "ToolCatalogV3",
                    ["tools"] = new JArray()
                };
            }
        }
        /*
        public JObject BuildToolDefinitionsJson_OLD2()
        {
            if (toolCatalogJson == null || string.IsNullOrWhiteSpace(toolCatalogJson.text))
            {
                return new JObject
                {
                    ["schema"] = "ToolCatalogV1",
                    ["tasks"] = new JArray()
                };
            }

            try
            {
                return JObject.Parse(toolCatalogJson.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ToolCatalogV1 parse failed: {ex.Message}");
                return new JObject
                {
                    ["schema"] = "ToolCatalogV1",
                    ["tasks"] = new JArray()
                };
            }
        }
        */

        /// <summary>
        /// Returns the JSON string describing the expected response shape.
        /// Source: TextAsset if provided, otherwise a minimal fallback.
        /// </summary>
        public JObject BuildResponseSchemaJson()
        {
            if (cachedResponseSchema != null)
                return (JObject)cachedResponseSchema.DeepClone();

            string resolvedResponseSchemaResourcePath = ResolveResponseSchemaResourcePath();
            if (responseSchemaJson == null || string.Equals(responseSchemaJson.name, "PlanResponseV1.schema", StringComparison.Ordinal))
                responseSchemaJson = Resources.Load<TextAsset>(resolvedResponseSchemaResourcePath) ?? responseSchemaJson;

            if (responseSchemaJson != null && !string.IsNullOrWhiteSpace(responseSchemaJson.text))
            {
                try
                {
                    cachedResponseSchema = JObject.Parse(responseSchemaJson.text);
                    return (JObject)cachedResponseSchema.DeepClone();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Response schema parse failed: {ex.Message}");
                }
            }

            // Minimal fallback that still tells the model what we want.
            var fallback = new JObject
            {
                ["schema"] = "PlanResponseV3",
                ["type"] = "object",
                ["required"] = new JArray("schema", "requestId", "agentId", "intentions"),
                ["properties"] = new JObject
                {
                    ["schema"] = new JObject { ["type"] = "string", ["const"] = "PlanResponseV3" },
                    ["requestId"] = new JObject { ["type"] = "string" },
                    ["agentId"] = new JObject { ["type"] = "string" },
                    ["plan_summary"] = new JObject { ["type"] = "string" },
                    ["intentions"] = new JObject { ["type"] = "array" },
                    ["questionsForNextContext"] = new JObject { ["type"] = "array" },
                    ["debug"] = new JObject { ["type"] = "object" }
                }
            };
            cachedResponseSchema = fallback;
            return (JObject)cachedResponseSchema.DeepClone();
        }

        private string ResolveToolCatalogResourcePath()
        {
            if (string.IsNullOrWhiteSpace(toolCatalogResourcePath) ||
                string.Equals(toolCatalogResourcePath, "ToolCatalogV1", StringComparison.Ordinal) ||
                string.Equals(toolCatalogResourcePath, "LLM/Tools/ToolCatalogV1", StringComparison.Ordinal))
            {
                return "LLM/Tools/ToolCatalogV3";
            }

            return toolCatalogResourcePath;
        }

        private string ResolveResponseSchemaResourcePath()
        {
            if (string.IsNullOrWhiteSpace(responseSchemaResourcePath) ||
                string.Equals(responseSchemaResourcePath, "PlanResponseV1.schema", StringComparison.Ordinal) ||
                string.Equals(responseSchemaResourcePath, "LLM/Schemas/PlanResponseV1.schema", StringComparison.Ordinal))
            {
                return "LLM/Schemas/PlanResponseV3.schema";
            }

            return responseSchemaResourcePath;
        }
    }
}
