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

        [SerializeField] private string toolCatalogResourcePath = "ToolCatalogV1"; // no extension

        private void Awake()
        {
            if (toolCatalogJson == null)
                toolCatalogJson = Resources.Load<TextAsset>(toolCatalogResourcePath);

            if (toolCatalogJson == null)
                Debug.LogError($"ToolCatalogV1.json not found. Expected Resources/{toolCatalogResourcePath}.json");
        }

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
        public JObject OLD_BuildToolDefinitionsJson()
        {
            //if (toolCatalogJson != null && !string.IsNullOrWhiteSpace(toolCatalogJson.text))
            //    return toolCatalogJson.text.Trim();

            // Minimal fallback so the field is never empty.
            // You can replace this later with a generator over your Tasks directory.
            var fallback = new JObject
            {
                ["schema"] = "ToolCatalogV1",
                ["tools"] = new JArray()
            };
            return fallback;
        }

        private const string ToolCatalogResourcePath = "LLM/Tools/ToolCatalogV1";
        private JObject cachedToolCatalog;

        public JObject BuildToolDefinitionsJson()
        {
            if (cachedToolCatalog != null)
                return (JObject)cachedToolCatalog.DeepClone();

            if (toolCatalogJson == null)
                toolCatalogJson = Resources.Load<TextAsset>(ToolCatalogResourcePath);

            if (toolCatalogJson == null)
            {
                Debug.LogError($"ToolCatalogV1.json not found at Resources/{ToolCatalogResourcePath}.json");

                return new JObject
                {
                    ["schema"] = "ToolCatalogV1",
                    ["tools"] = new JArray()
                };
            }

            try
            {
                var parsed = JObject.Parse(toolCatalogJson.text);

                var result = new JObject
                {
                    ["schema"] = parsed.Value<string>("schema") ?? "ToolCatalogV1",
                    ["description"] = parsed.Value<string>("description") ?? "",
                    ["criticalRules"] = parsed["criticalRules"]?.DeepClone(),
                    ["taskParameterConvention"] = parsed["taskParameterConvention"]?.DeepClone(),
                    ["tools"] = parsed["tasks"]?.DeepClone() ?? new JArray()
                };

                cachedToolCatalog = result;
                return (JObject)cachedToolCatalog.DeepClone();
            }
            catch (Exception ex)
            {
                Debug.LogError($"ToolCatalogV1 parse failed: {ex.Message}");

                return new JObject
                {
                    ["schema"] = "ToolCatalogV1",
                    ["tools"] = new JArray()
                };
            }
        }
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

        /// <summary>
        /// Returns the JSON string describing the expected response shape.
        /// Source: TextAsset if provided, otherwise a minimal fallback.
        /// </summary>
        public JObject BuildResponseSchemaJson()
        {
            //if (responseSchemaJson != null && !string.IsNullOrWhiteSpace(responseSchemaJson.text))
            //    return responseSchemaJson.text.Trim();

            // Minimal fallback that still tells the model what we want.
            var fallback = new JObject
            {
                ["schema"] = "PlanResponseV1",
                ["type"] = "object",
                ["required"] = new JArray("schema", "requestId", "agentId", "intentions", "debug"),
                ["properties"] = new JObject
                {
                    ["schema"] = new JObject { ["type"] = "string", ["const"] = "PlanResponseV1" },
                    ["requestId"] = new JObject { ["type"] = "string" },
                    ["agentId"] = new JObject { ["type"] = "string" },
                    ["intentions"] = new JObject { ["type"] = "array" },
                    ["questionsForNextContext"] = new JObject { ["type"] = "array" },
                    ["debug"] = new JObject { ["type"] = "object" }
                }
            };
            return fallback;
        }
    }
}