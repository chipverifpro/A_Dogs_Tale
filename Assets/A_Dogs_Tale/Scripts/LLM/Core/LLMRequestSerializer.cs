#nullable enable
using Newtonsoft.Json;

namespace DogGame.LLM.Core
{
    public static class LLMRequestSerializer
    {
        public static string ToJson(LLMRequest request)
        {
            // Pretty-print helps debugging; you can switch Formatting.None later.
            return JsonConvert.SerializeObject(request, Formatting.Indented);
        }
    }
}