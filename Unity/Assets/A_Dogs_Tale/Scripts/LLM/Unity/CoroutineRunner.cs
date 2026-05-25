#nullable enable
using UnityEngine;

namespace DogGame.LLM.Unity
{
    /// <summary>
    /// Allows non-MonoBehaviour code to run coroutines and return Tasks.
    /// Put this on a GameObject in your bootstrap scene (or auto-create it).
    /// </summary>
    public sealed class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner? instance;

        /*
        public static CoroutineRunner Instance
        {
            get
            {
                if (instance != null) return instance;

                var gameObject = new GameObject("CoroutineRunner");
                DontDestroyOnLoad(gameObject);
                instance = gameObject.AddComponent<CoroutineRunner>();
                return instance;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        public Task Run(IEnumerator routine)
        {
            if (routine == null) throw new ArgumentNullException(nameof(routine));

            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(RunInternal(routine, tcs));
            return tcs.Task;
        }

        private static IEnumerator RunInternal(IEnumerator routine, TaskCompletionSource<bool> tcs)
        {
            Exception? exception = null;

            while (true)
            {
                object? current;
                try
                {
                    if (!routine.MoveNext())
                        break;

                    current = routine.Current;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    break;
                }

                yield return current;
            }

            if (exception != null) tcs.SetException(exception);
            else tcs.SetResult(true);
        }
        */
    }
}
