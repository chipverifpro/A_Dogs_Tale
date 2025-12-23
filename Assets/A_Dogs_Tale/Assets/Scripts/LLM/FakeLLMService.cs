#nullable enable
using System;
using System.Collections;
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class FakeLLMService : MonoBehaviour
    {
        private Vector2 latencyRangeSeconds = new(0.4f, 1.4f);
        private int planCounter;

        public void SetLatencyRange(Vector2 secondsRange)
        {
            latencyRangeSeconds = new Vector2(
                Mathf.Max(0f, Mathf.Min(secondsRange.x, secondsRange.y)),
                Mathf.Max(0f, Mathf.Max(secondsRange.x, secondsRange.y)));
        }

        public void SubmitRequest(string requestId, string requestJson, string agentId, Action<string> onResponseJson)
        {
            StartCoroutine(RespondLaterCoroutine(requestId, agentId, onResponseJson));
        }

        private IEnumerator RespondLaterCoroutine(string requestId, string agentId, Action<string> onResponseJson)
        {
            float delaySeconds = UnityEngine.Random.Range(latencyRangeSeconds.x, latencyRangeSeconds.y);
            yield return new WaitForSeconds(delaySeconds);

            string responseJson = BuildCannedPlanResponse(requestId, agentId, planCounter++);
            onResponseJson?.Invoke(responseJson);
        }

        private static string BuildCannedPlanResponse(string requestId, string agentId, int counter)
        {
            // Cycle through a few deterministic plans so you can see it repeatedly working.
            // NOTE: This returns PlanResponseV1 JSON (what your parser expects).
            int mode = counter % 3;

            string move1 = mode switch
            {
                0 => "\"parameters\":{ \"task\":\"move_to_cell\", \"locationCell\":[5,3], \"stopRadius\":0.2 }",
                1 => "\"parameters\":{ \"task\":\"move_to_cell\", \"locationCell\":[2,8], \"stopRadius\":0.2 }",
                _ => "\"parameters\":{ \"task\":\"move_to_cell\", \"locationCell\":[8,2], \"stopRadius\":0.2 }",
            };

            string move2 = mode switch
            {
                0 => "\"parameters\":{ \"task\":\"move_to_cell\", \"locationCell\":[2,8], \"stopRadius\":0.2 }",
                1 => "\"parameters\":{ \"task\":\"move_to_cell\", \"locationCell\":[8,2], \"stopRadius\":0.2 }",
                _ => "\"parameters\":{ \"task\":\"move_to_cell\", \"locationCell\":[5,3], \"stopRadius\":0.2 }",
            };

            return
                "{"
                + "\"schema\":\"PlanResponseV1\","
                + $"\"requestId\":\"{requestId}\","
                + $"\"agentId\":\"{agentId}\","
                + "\"intentions\":["
                + "{ \"type\":\"add_task\", \"id\":\"m1\", \"priority\":0.9, " + move1 + " },"
                + "{ \"type\":\"add_task\", \"id\":\"w1\", \"priority\":0.5, \"parameters\":{ \"task\":\"wait\", \"seconds\":0.8 } },"
                + "{ \"type\":\"add_task\", \"id\":\"m2\", \"priority\":0.8, " + move2 + " }"
                + "],"
                + "\"debug\":{ \"confidence\":0.65, \"notes\":[\"fake async responder\"] }"
                + "}";
        }
    }
}







// =============== Old Version ===============
namespace DogGame.LLM
{
    public interface ILLMService
    {
        void SubmitRequest(string requestId, string requestJson, Action<string> onResponseJson);
    }

    public sealed class FakeLLMService_V1 : MonoBehaviour, ILLMService
    {
        [SerializeField] private float simulatedLatencySeconds = 0.75f;

        public void SubmitRequest(string requestId, string requestJson, Action<string> onResponseJson)
        {
            StartCoroutine(RespondLaterCoroutine(requestId, onResponseJson));
        }

        private IEnumerator RespondLaterCoroutine(string requestId, Action<string> onResponseJson)
        {
            yield return new WaitForSeconds(simulatedLatencySeconds);

            // Minimal valid PlanResponseV1 JSON:
            string responseJson =
                "{"
                + "\"schema\":\"PlanResponseV1\","
                + $"\"requestId\":\"{requestId}\","
                + "\"agentId\":\"npc_023\","
                + "\"intentions\":["
                + "  {"
                + "    \"type\":\"set_goal\","
                + "    \"id\":\"goal_confirm_route\","
                + "    \"priority\":0.8,"
                + "    \"rationale\":\"Test plan: confirm route before engaging.\","
                + "    \"parameters\":{ \"goal\":\"confirm_intruder_route\", \"horizonSeconds\":120 }"
                + "  }"
                + "],"
                + "\"debug\":{ \"confidence\":0.7, \"notes\":[\"fake responder\"] }"
                + "}";

            onResponseJson?.Invoke(responseJson);
        }
    }
}