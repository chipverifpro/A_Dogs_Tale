#nullable enable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class LLMJsonNormalizer
{
    public static string Normalize(string jsonMaybe)
    {
        if (string.IsNullOrWhiteSpace(jsonMaybe))
            return "";

        // Case 1: already JSON
        if (TryPretty(jsonMaybe, out var pretty))
            return pretty;

        // Case 2: JSON string literal that contains JSON
        try
        {
            string? unescaped = JsonConvert.DeserializeObject<string>(jsonMaybe);
            if (!string.IsNullOrWhiteSpace(unescaped) &&
                TryPretty(unescaped, out pretty))
            {
                return pretty;
            }
        }
        catch { }

        // Fallback
        return jsonMaybe;
    }

    private static bool TryPretty(string json, out string pretty)
    {
        pretty = "";
        try
        {
            var token = JToken.Parse(json);
            pretty = token.ToString(Formatting.Indented);
            return true;
        }
        catch
        {
            return false;
        }
    }
}