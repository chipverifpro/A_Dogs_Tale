using System.Collections.Generic;
using UnityEngine;


public class BlackboardModule : WorldModule
{
    private readonly Dictionary<string, object> data = new();

    public void Set<T>(string key, T value)
    {
        data[key] = value;
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (data.TryGetValue(key, out var obj) && obj is T cast)
        {
            value = cast;
            return true;
        }

        value = default;
        return false;
    }

    public bool HasKey(string key) => data.ContainsKey(key);
    public void Remove(string key) => data.Remove(key);
}
