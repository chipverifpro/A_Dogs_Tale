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

            // If this client tracks cooldown, avoid retrying/spamming during cooldown
            if (this is ICooldownAware cooldownAware && cooldownAware.IsCoolingDown)
            {
                return new LLMResponse
                {
                    succeeded = false,
                    isRateLimited = true,
                    retryAfterSeconds = cooldownAware.CooldownRemainingSeconds,
                    errorMessage = $"[{Vendor}] Cooling down ({cooldownAware.CooldownRemainingSeconds:0.0}s). Skipping requestId={request.requestId}."
                };
            }

            const int maxAttempts = 3;
            float backoffSeconds = 0.5f;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Re-check cooldown between attempts
                if (this is ICooldownAware cooldownAware2 && cooldownAware2.IsCoolingDown)
                {
                    return new LLMResponse
                    {
                        succeeded = false,
                        isRateLimited = true,
                        retryAfterSeconds = cooldownAware2.CooldownRemainingSeconds,
                        errorMessage = $"[{Vendor}] Cooling down ({cooldownAware2.CooldownRemainingSeconds:0.0}s). Skipping requestId={request.requestId}."
                    };
                }

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

                    // ✅ Critical: if provider says it's rate-limited, STOP retrying automatically
                    if (response.isRateLimited)
                    {
                        // Optionally log once
                        Debug.LogWarning($"[{Vendor}] Rate limited. retryAfter={response.retryAfterSeconds:0.0}s requestId={request.requestId}");
                        return response;
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