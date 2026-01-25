using UnityEditor.PackageManager.Requests;
using UnityEngine;
using DogGame.Attributes;

// A little momitor to display the LLM input/output packets in the Unity browser.
public class LLMDebugMonitor : MonoBehaviour
{
    [Header("Request")]
    public float Time_Request;
    //[TextArea(3, 12)]
    [JsonPreview(260f)]
    public string LLM_Request;
    [JsonPreview(260f)]
    public string LLM_Request_Input;

    [Header("Response")]
    public float Time_Response;
    //[TextArea(3, 12)]
    [JsonPreview(260f)]
    public string LLM_Response;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        LLM_Request = "";
        LLM_Request_Input = "";
        LLM_Response = "";
    }

    public void DebugLLMRequest_Input(string request)
    {
        LLM_Request_Input = request;
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
