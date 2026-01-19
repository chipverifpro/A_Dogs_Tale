#nullable enable
using System.Threading;

namespace DogGame.LLM.Core
{
    /// <summary>
    /// Global epoch used to invalidate late LLM responses after play stop / disable / restart.
    /// Any code can call Bump() to invalidate in-flight responses.
    /// </summary>
    public static class LLMSessionToken
    {
        private static int token = 0;

        public static int Current => Volatile.Read(ref token);

        public static int Bump() => Interlocked.Increment(ref token);
    }
}