using System;

namespace DogGame.LLM.Core
{
    public static class LLMRequestId
    {
        public static string NewShortHex()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 4);
        }
    }
}
