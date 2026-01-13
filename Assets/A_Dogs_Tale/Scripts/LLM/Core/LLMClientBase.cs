using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DogGame.LLM.Core
{
    public abstract class LLMClientBase : ILLMClient
    {
        public abstract string Vendor { get; }

        public async Task<LLMResponse> SendAsync(LLMRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.profile == null) throw new ArgumentNullException(nameof(request.profile));

            // Light retry for transient errors (429, timeouts, etc.)
            const int maxAttempts = 3;
            float backoffSeconds = 0.5f;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    LLMResponse response = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
                    if (response == null)
                    {
                        return new LLMResponse
                        {
                            succeeded = false,
                            errorMessage = $"{Vendor}: null response"
                        };
                    }

                    return response;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    bool isLastAttempt = attempt == maxAttempts;
                    Debug.LogWarning($"[{Vendor}] LLM attempt {attempt}/{maxAttempts} failed: {exception.Message}");

                    if (isLastAttempt)
                    {
                        return new LLMResponse
                        {
                            succeeded = false,
                            errorMessage = $"{Vendor}: {exception.GetType().Name}: {exception.Message}"
                        };
                    }

                    // simple exponential backoff
                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), cancellationToken).ConfigureAwait(false);
                    backoffSeconds *= 2f;
                }
            }

            return new LLMResponse
            {
                succeeded = false,
                errorMessage = $"{Vendor}: unexpected fallthrough"
            };
        }

        protected abstract Task<LLMResponse> SendCoreAsync(LLMRequest request, CancellationToken cancellationToken);
    }
}