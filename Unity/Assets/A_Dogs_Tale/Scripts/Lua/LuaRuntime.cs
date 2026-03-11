using MoonSharp.Interpreter;

public class LuaRuntime
{
    private Script script;

    public LuaRuntime()
    {
        script = new Script();

        // Optional sandbox restrictions
        script.Options.DebugPrint = s => UnityEngine.Debug.Log("[Lua] " + s);
    }

    public void RegisterFunction(string name, object func)
    {
        script.Globals[name] = func;
    }

    public void SetGlobal(string name, object value)
    {
        script.Globals[name] = value;
    }

    public DynValue Execute(string code)
    {
        return script.DoString(code);
    }

    public DynValue Call(string functionName)
    {
        return script.Call(script.Globals[functionName]);
    }
}