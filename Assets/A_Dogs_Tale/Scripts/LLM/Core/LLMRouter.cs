using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DogGame.LLM.Core
{
    public sealed class LLMRouter
    {
        private readonly Dictionary<string, ILLMClient> clientsByVendor = new(StringComparer.OrdinalIgnoreCase);

        public LLMRouter(IEnumerable<ILLMClient> clients)
        {
            foreach (var client in clients)
            {
                if (client == null) continue;
                clientsByVendor[client.Vendor] = client;
            }
        }

        public Task<LLMResponse> SendAsync(LLMRequest request, CancellationToken cancellationToken)
        {
            if (request?.profile == null) throw new ArgumentNullException(nameof(request));

            if (!clientsByVendor.TryGetValue(request.profile.vendor ?? "", out var client))
                throw new InvalidOperationException($"No LLM client registered for vendor '{request.profile.vendor}'.");

            return client.SendAsync(request, cancellationToken);
        }
    }
}