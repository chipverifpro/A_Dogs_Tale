#nullable enable
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DogGame.LLM.Debugging
{
    public static class LLMPacketLogger
    {
        private static readonly string RootDir;

        static LLMPacketLogger()
        {
            // Cross-platform temp location
            RootDir = Path.Combine(Application.temporaryCachePath, "LLM_Packets");

            try
            {
                System.IO.Directory.CreateDirectory(RootDir);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LLMPacketLogger] Failed to create root dir: {ex.Message}");
            }
        }

        /*
        public static void LogRequest(
            string agentId,
            string requestId,
            string provider,
            string requestJson)
        {
            Dir.Instance.llmDebugMonitor.DebugLLMRequest(requestJson, agentId, requestId);
            WritePacket(
                agentId,
                requestId,
                provider,
                kind: "request",
                payload: requestJson);
        }
        */

        public static void LogResponse(
            string agentId,
            string requestId,
            string provider,
            string responseJson)
        {
            if (Dir.Instance != null && Dir.Instance.llmDebugMonitor != null)
                Dir.Instance.llmDebugMonitor.DebugLLMResponse(responseJson, agentId, requestId, wasStale:false);

            WritePacket(
                agentId,
                requestId,
                provider,
                kind: "response",
                payload: responseJson);
        }

        private static void WritePacket(
            string agentId,
            string requestId,
            string provider,
            string kind,
            string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return;

            try
            {
                string safeAgent = Sanitize(agentId);
                string safeReq = Sanitize(requestId);

                string dir = Path.Combine(
                    RootDir,
                    safeAgent,
                    safeReq);

                System.IO.Directory.CreateDirectory(dir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string file = $"{timestamp}_{provider}_{kind}.json";

                string path = Path.Combine(dir, file);

                Debug.Log($"WritePacket {path}");
                Debug.Log($"file contents:\n{payload}");
                
                File.WriteAllText(path, payload, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LLMPacketLogger] Write failed: {ex.Message}");
            }
        }

        private static string Sanitize(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }
    }
}
