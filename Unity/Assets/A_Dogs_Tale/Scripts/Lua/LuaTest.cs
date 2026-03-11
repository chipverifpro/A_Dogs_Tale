using MoonSharp.Interpreter;
using UnityEngine;

public class LuaTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("LuaTest Start entered");

        try
        {
            Script script = new Script();
            script.Options.DebugPrint = message => Debug.Log("[Lua] " + message);

            script.DoString("print('Lua initialized')");
            
            Debug.Log("LuaTest Start finished");
        }
        catch (System.Exception exception)
        {
            Debug.LogError("LuaTest exception: " + exception);
        }
    }
}