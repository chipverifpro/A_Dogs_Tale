#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using DogGame.LLM.Agent;
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class UnitySidecarInboundServer : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private int port = 8081;
        [SerializeField] private bool autoStartOnEnable = true;

        private HttpListener? httpListener;
        private Thread? listenerThread;
        private volatile bool isRunning;

        private readonly ConcurrentQueue<PendingRequest> pendingRequests = new();

        [Serializable]
        private sealed class WorldStateRequest
        {
            public string agent_id = "";
            public string detail = "normal";
        }

        [Serializable]
        private sealed class WorldStateResponse
        {
            public string world_state_text = "";
        }

        private sealed class PendingRequest
        {
            public string path = "";
            public string body = "";
            public HttpListenerContext? context;
            public readonly AutoResetEvent completed = new(false);
            public string responseJson = "";
            public int statusCode = 200;
            public string contentType = "application/json";
        }

        private void OnEnable()
        {
            if (autoStartOnEnable)
                StartServer();
        }

        private void OnDisable()
        {
            StopServer();
        }

        private void Update()
        {
            while (pendingRequests.TryDequeue(out PendingRequest? pendingRequest))
            {
                if (pendingRequest == null)
                    continue;

                try
                {
                    HandleRequestOnMainThread(pendingRequest);
                }
                catch (Exception exception)
                {
                    pendingRequest.statusCode = 500;
                    pendingRequest.responseJson =
                        "{\"error\":\"Unity exception while handling request\",\"details\":\""
                        + EscapeJson(exception.Message) + "\"}";
                }
                finally
                {
                    pendingRequest.completed.Set();
                }
            }
        }

        [ContextMenu("Start Sidecar Inbound Server")]
        public void StartServer()
        {
            if (isRunning)
                return;

            httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://127.0.0.1:{port}/");
            httpListener.Start();

            isRunning = true;

            listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "UnitySidecarInboundServer"
            };
            listenerThread.Start();

            Debug.Log($"[UnitySidecarInboundServer] Listening on http://127.0.0.1:{port}/");
        }

        [ContextMenu("Stop Sidecar Inbound Server")]
        public void StopServer()
        {
            if (!isRunning)
                return;

            isRunning = false;

            try
            {
                httpListener?.Stop();
                httpListener?.Close();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[UnitySidecarInboundServer] Stop warning: {exception.Message}");
            }

            httpListener = null;
            listenerThread = null;

            Debug.Log("[UnitySidecarInboundServer] Stopped.");
        }

        private void ListenLoop()
        {
            while (isRunning && httpListener != null)
            {
                try
                {
                    HttpListenerContext context = httpListener.GetContext();

                    string requestBody;
                    using (StreamReader reader = new(context.Request.InputStream, context.Request.ContentEncoding))
                    {
                        requestBody = reader.ReadToEnd();
                    }

                    PendingRequest pendingRequest = new()
                    {
                        path = context.Request.Url?.AbsolutePath ?? "/",
                        body = requestBody,
                        context = context
                    };

                    pendingRequests.Enqueue(pendingRequest);

                    pendingRequest.completed.WaitOne();

                    WriteResponse(
                        context.Response,
                        pendingRequest.statusCode,
                        pendingRequest.responseJson,
                        pendingRequest.contentType
                    );
                }
                catch (HttpListenerException)
                {
                    if (!isRunning)
                        break;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[UnitySidecarInboundServer] Listener exception: {exception}");
                }
            }
        }

        private void HandleRequestOnMainThread(PendingRequest pendingRequest)
        {
            if (pendingRequest.path.Equals("/world_state", StringComparison.OrdinalIgnoreCase))
            {
                HandleWorldStateRequest(pendingRequest);
                return;
            }

            pendingRequest.statusCode = 404;
            pendingRequest.responseJson = "{\"error\":\"Unknown endpoint\"}";
        }

        private void HandleWorldStateRequest(PendingRequest pendingRequest)
        {
            Debug.Log($"[UnitySidecarInboundServer] /world_state request body: {pendingRequest.body}");

            WorldStateRequest request = JsonUtility.FromJson<WorldStateRequest>(pendingRequest.body);

            if (request == null || string.IsNullOrWhiteSpace(request.agent_id))
            {
                pendingRequest.statusCode = 400;
                pendingRequest.responseJson = "{\"error\":\"Missing agent_id\"}";
                Debug.LogWarning("[UnitySidecarInboundServer] Missing agent_id.");
                return;
            }

            string detail = NormalizeDetail(request.detail);
            Debug.Log($"[UnitySidecarInboundServer] Looking up agent '{request.agent_id}' with detail '{detail}'.");

            LLMWorldStateModule? worldStateModule = FindWorldStateModule(request.agent_id);

            if (worldStateModule == null)
            {
                pendingRequest.statusCode = 404;
                pendingRequest.responseJson =
                    "{\"error\":\"Agent not found\",\"agent_id\":\"" + EscapeJson(request.agent_id) + "\"}";
                Debug.LogWarning($"[UnitySidecarInboundServer] Agent not found: {request.agent_id}");
                return;
            }

            Debug.Log($"[UnitySidecarInboundServer] Found module on GameObject '{worldStateModule.gameObject.name}' for agent '{request.agent_id}'.");
            Debug.Log("[UnitySidecarInboundServer] Building world state text...");

            string worldStateText = worldStateModule.BuildWorldStateText(detail);

            Debug.Log($"[UnitySidecarInboundServer] World state text built successfully. Length={worldStateText.Length}");

            WorldStateResponse response = new()
            {
                world_state_text = worldStateText
            };

            pendingRequest.statusCode = 200;
            pendingRequest.responseJson = JsonUtility.ToJson(response, prettyPrint: true);

            Debug.Log("[UnitySidecarInboundServer] /world_state response ready.");
        }

        private static string NormalizeDetail(string? detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
                return "normal";

            string normalized = detail.Trim().ToLowerInvariant();

            return normalized switch
            {
                "brief" => "brief",
                "detailed" => "detailed",
                _ => "normal"
            };
        }

        private static LLMWorldStateModule? FindWorldStateModule(string agentId)
        {
            LLMWorldStateModule[] modules = FindObjectsByType<LLMWorldStateModule>(FindObjectsSortMode.None);

            foreach (LLMWorldStateModule module in modules)
            {
                if (module == null)
                    continue;

                if (module.worldObject != null)
                {
                    if (string.Equals(module.worldObject.DisplayName, agentId, StringComparison.OrdinalIgnoreCase))
                        return module;
                }

                if (string.Equals(module.gameObject.name, agentId, StringComparison.OrdinalIgnoreCase))
                    return module;
            }

            return null;
        }

        private static void WriteResponse(HttpListenerResponse response, int statusCode, string body, string contentType)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(body ?? "");

            response.StatusCode = statusCode;
            response.ContentType = contentType;
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;

            using Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
        }

        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}