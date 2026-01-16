using UnityEditor.PackageManager.Requests;
using UnityEngine;

// A little momitor to display the LLM input/output packets in the Unity browser.
public class LLMDebugMonitor : MonoBehaviour
{
    [Header("Request")]
    public float Time_Request;
    [TextArea(3, 12)]
    public string LLM_Request;

    [Header("Response")]
    public float Time_Response;
    [TextArea(3, 12)]
    public string LLM_Response;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        LLM_Request = "";
        LLM_Response = "";
    }

    public void DebugLLMRequest(string request)
    {
        LLM_Request = request;
        Time_Request = Time.time;
    }

    public void DebugLLMResponse(string response)
    {
        LLM_Response = response;
        Time_Response = Time.time;
    }
}
