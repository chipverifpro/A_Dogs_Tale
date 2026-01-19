#nullable enable
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

            // Capture token at dispatch. If it changes later, this response is stale.
            int tokenAtDispatch = LLMSessionToken.Current;

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

                // If play session changed, stop immediately.
                if (LLMSessionToken.Current != tokenAtDispatch)
                {
                    return new LLMResponse
                    {
                        succeeded = false,
                        wasStale = true,
                        errorMessage = $"[{Vendor}] Ignored stale response (session changed) requestId={request.requestId}."
                    };
                }

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

                    // Critical: provider returned, but session may have changed while it was running.
                    if (LLMSessionToken.Current != tokenAtDispatch)
                    {
                        return new LLMResponse
                        {
                            succeeded = false,
                            wasStale = true,
                            errorMessage = $"[{Vendor}] Ignored stale response (session changed after provider return) requestId={request.requestId}."
                        };
                    }

                    if (response == null)
                    {
                        return new LLMResponse
                        {
                            succeeded = false,
                            errorMessage = $"{Vendor}: null response"
                        };
                    }

                    // If provider says rate-limited, STOP retrying automatically.
                    if (response.isRateLimited)
                    {
                        Debug.LogWarning($"[{Vendor}] Rate limited. retryAfter={response.retryAfterSeconds:0.0}s requestId={request.requestId}");
                        return response;
                    }

                    return response;
                }
                catch (OperationCanceledException)
                {
                    // If cancellation happened because of a session change, prefer stale response.
                    if (LLMSessionToken.Current != tokenAtDispatch)
                    {
                        return new LLMResponse
                        {
                            succeeded = false,
                            wasStale = true,
                            errorMessage = $"[{Vendor}] Canceled due to session change requestId={request.requestId}."
                        };
                    }

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

                    // Backoff, but don’t waste time if session changed during the delay.
                    float delaySeconds = backoffSeconds;
                    backoffSeconds *= 2f;

                    const float sliceSeconds = 0.1f;
                    float remaining = delaySeconds;

                    while (remaining > 0f)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (LLMSessionToken.Current != tokenAtDispatch)
                        {
                            return new LLMResponse
                            {
                                succeeded = false,
                                wasStale = true,
                                errorMessage = $"[{Vendor}] Ignored stale response during backoff (session changed) requestId={request.requestId}."
                            };
                        }

                        float step = Math.Min(sliceSeconds, remaining);
                        await Task.Delay(TimeSpan.FromSeconds(step), cancellationToken).ConfigureAwait(false);
                        remaining -= step;
                    }
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