using UnityEngine;
using Newtonsoft.Json.Linq;

public class JsonSmokeTest : MonoBehaviour
{
    void Start()
    {
        var obj = new JObject();
        obj["test"] = 123;
        Debug.Log(obj.ToString());
    }
}