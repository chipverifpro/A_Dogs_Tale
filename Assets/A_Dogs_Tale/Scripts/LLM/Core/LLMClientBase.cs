// Assets/A_Dogs_Tale/Scripts/LLM/Core/LLMClientBase.cs
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

        // ---- Session token stale-response gate ----
        // Increment this when Play stops / restarts, or when you want all inflight results to become no-ops.
        private static int sessionToken;

        public static int CurrentSessionToken => sessionToken;

        public static int BeginNewSession()
        {
            // wraps naturally; comparisons remain correct for "changed or not changed"
            sessionToken++;
            return sessionToken;
        }

        public async Task<LLMResponse> SendAsync(LLMRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.profile == null) throw new ArgumentNullException(nameof(request.profile));

            int capturedSessionToken = CurrentSessionToken;

            // If this client tracks cooldown, avoid retrying/spamming during cooldown
            if (this is ICooldownAware cooldownAware && cooldownAware.IsCoolingDown)
            {
                return MarkStaleIfNeeded(capturedSessionToken, new LLMResponse
                {
                    succeeded = false,
                    isRateLimited = true,
                    retryAfterSeconds = cooldownAware.CooldownRemainingSeconds,
                    errorMessage = $"[{Vendor}] Cooling down ({cooldownAware.CooldownRemainingSeconds:0.0}s). Skipping requestId={request.requestId}."
                });
            }

            const int maxAttempts = 3;
            float backoffSeconds = 0.5f;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Re-check cooldown between attempts
                if (this is ICooldownAware cooldownAware2 && cooldownAware2.IsCoolingDown)
                {
                    return MarkStaleIfNeeded(capturedSessionToken, new LLMResponse
                    {
                        succeeded = false,
                        isRateLimited = true,
                        retryAfterSeconds = cooldownAware2.CooldownRemainingSeconds,
                        errorMessage = $"[{Vendor}] Cooling down ({cooldownAware2.CooldownRemainingSeconds:0.0}s). Skipping requestId={request.requestId}."
                    });
                }

                try
                {
                    LLMResponse response = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);

                    if (response == null)
                    {
                        return MarkStaleIfNeeded(capturedSessionToken, new LLMResponse
                        {
                            succeeded = false,
                            errorMessage = $"{Vendor}: null response"
                        });
                    }

                    // ✅ Stop retrying automatically on rate limit
                    if (response.isRateLimited)
                    {
                        Debug.LogWarning($"[{Vendor}] Rate limited. retryAfter={response.retryAfterSeconds:0.0}s requestId={request.requestId}");
                        return MarkStaleIfNeeded(capturedSessionToken, response);
                    }

                    return MarkStaleIfNeeded(capturedSessionToken, response);
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
                        return MarkStaleIfNeeded(capturedSessionToken, new LLMResponse
                        {
                            succeeded = false,
                            errorMessage = $"{Vendor}: {exception.GetType().Name}: {exception.Message}"
                        });
                    }

                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), cancellationToken).ConfigureAwait(false);
                    backoffSeconds *= 2f;
                }
            }

            return MarkStaleIfNeeded(capturedSessionToken, new LLMResponse
            {
                succeeded = false,
                errorMessage = $"{Vendor}: unexpected fallthrough"
            });
        }

        protected abstract Task<LLMResponse> SendCoreAsync(LLMRequest request, CancellationToken cancellationToken);

        private static LLMResponse MarkStaleIfNeeded(int capturedToken, LLMResponse response)
        {
            if (response == null) return null;

            // If session changed since request began, mark stale so upstream can drop it.
            if (capturedToken != CurrentSessionToken)
            {
                response.wasStale = true;

                // Optional: if you prefer stale responses to be treated as failures:
                // response.succeeded = false;
                // response.errorMessage = "Stale response (session changed).";
            }

            return response;
        }
    }
}