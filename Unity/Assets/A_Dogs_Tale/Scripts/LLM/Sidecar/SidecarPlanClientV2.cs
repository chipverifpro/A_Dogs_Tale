using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DogGame.LLM
{
    public sealed class SidecarPlanClientV2 : MonoBehaviour
    {
        [SerializeField] private AgentPlanExecutor planExecutor;

        [Header("Connection")]
        [SerializeField] private string planUrl = "http://127.0.0.1:8000/plan";
        [SerializeField] private float requestTimeoutSeconds = 10f;

        // ------------------------------------------------------------
        // Public entry point
        // ------------------------------------------------------------

        public void RequestDebugPlan()
        {
            Debug.Log("[SidecarPlanClientV2] RequestDebugPlan called.");

            PlanRequestV2 request = BuildDebugRequest();

            StartCoroutine(SendPlanRequestCoroutine(request));
        }

        // ------------------------------------------------------------
        // Build simple debug request
        // ------------------------------------------------------------

        private PlanRequestV2 BuildDebugRequest()
        {
            return new PlanRequestV2
            {
                schema = "plan_request_v2",
                agent_id = "dog_1",

                trigger = new TriggerV2
                {
                    type = "DebugTest",
                    location = new[] { 0f, 0f, 0f },
                    intensity = 1f
                },

                world_state = new WorldStateV2
                {
                    position = new[] { 0f, 0f, 0f },
                    pack_members_nearby = 0
                },

                constraints = new ConstraintsV2
                {
                    max_plan_steps = 4,
                    max_latency_ms = 1000
                }
            };
        }

        // ------------------------------------------------------------
        // HTTP request coroutine
        // ------------------------------------------------------------

        private IEnumerator SendPlanRequestCoroutine(PlanRequestV2 request)
        {
            Debug.Log("[SidecarPlanClientV2] SendPlanRequestCoroutine started.");

            string requestJson = JsonUtility.ToJson(request, true);

            Debug.Log("[SidecarPlanClientV2] Request JSON:\n" + requestJson);

            using UnityWebRequest webRequest = new(planUrl, UnityWebRequest.kHttpVerbPOST);

            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);

            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            webRequest.SetRequestHeader("Content-Type", "application/json");

            webRequest.timeout = Mathf.CeilToInt(requestTimeoutSeconds);

            Debug.Log($"[SidecarPlanClientV2] Sending POST to {planUrl}");

            yield return webRequest.SendWebRequest();

            Debug.Log("[SidecarPlanClientV2] SendWebRequest returned.");

            Debug.Log($"[SidecarPlanClientV2] Result: {webRequest.result}");
            Debug.Log($"[SidecarPlanClientV2] Response Code: {webRequest.responseCode}");

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[SidecarPlanClientV2] Request failed: " + webRequest.error);
                yield break;
            }

            string responseJson = webRequest.downloadHandler.text;

            Debug.Log("[SidecarPlanClientV2] Raw response:\n" + responseJson);

            if (!TryParseAndValidateResponse(responseJson, out PlanResponseV2 response))
            {
                Debug.LogError("[SidecarPlanClientV2] Response validation failed.");
                yield break;
            }

            if (planExecutor == null)
            {
                Debug.LogError("[SidecarPlanClientV2] No AgentPlanExecutor assigned.");
                yield break;
            }

            planExecutor.ApplyPlan(response);
        }

        // ------------------------------------------------------------
        // Validate schema and parse response
        // ------------------------------------------------------------

        private bool TryParseAndValidateResponse(string responseJson, out PlanResponseV2 response)
        {
            response = null;

            SchemaProbeV2 probe;

            try
            {
                probe = JsonUtility.FromJson<SchemaProbeV2>(responseJson);
            }
            catch (Exception exception)
            {
                Debug.LogError("[SidecarPlanClientV2] Schema probe failed: " + exception);
                return false;
            }

            if (probe == null || probe.schema != "plan_response_v2")
            {
                Debug.LogError("[SidecarPlanClientV2] Unexpected schema: " + probe?.schema);
                return false;
            }

            try
            {
                response = JsonUtility.FromJson<PlanResponseV2>(responseJson);
            }
            catch (Exception exception)
            {
                Debug.LogError("[SidecarPlanClientV2] Full parse failed: " + exception);
                return false;
            }

            if (response == null)
            {
                Debug.LogError("[SidecarPlanClientV2] Parsed response is null.");
                return false;
            }

            if (response.intentions == null || response.intentions.Count == 0)
            {
                Debug.LogWarning("[SidecarPlanClientV2] Response contained no intentions.");
                return false;
            }

            Debug.Log($"[SidecarPlanClientV2] Validated schema '{response.schema}' with {response.intentions.Count} intention(s).");

            return true;
        }
    }
}