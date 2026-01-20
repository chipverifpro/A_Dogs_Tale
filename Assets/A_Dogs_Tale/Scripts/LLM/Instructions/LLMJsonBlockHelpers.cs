#nullable enable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class LLMJsonBlockHelpers
{
    public static string NormalizeJsonForPrompt(string jsonMaybe)
    {
        if (string.IsNullOrWhiteSpace(jsonMaybe))
            return "";

        // Case 1: already JSON object/array
        if (TryFormatJson(jsonMaybe, out var formatted))
            return formatted;

        // Case 2: JSON string literal that contains JSON
        try
        {
            string? unescaped = JsonConvert.DeserializeObject<string>(jsonMaybe);
            if (!string.IsNullOrWhiteSpace(unescaped))
            {
                if (TryFormatJson(unescaped, out formatted))
                    return formatted;

                return unescaped;
            }
        }
        catch
        {
            // ignore
        }

        return jsonMaybe;
    }

    private static bool TryFormatJson(string json, out string formatted)
    {
        formatted = "";
        try
        {
            var token = JToken.Parse(json);
            formatted = token.ToString(Formatting.Indented);
            return true;
        }
        catch
        {
            return false;
        }
    }
}