using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;

namespace DogGame.LLM.Providers
{
    public sealed class OpenAIClient : LLMClientBase
    {
        public override string Vendor => "OpenAI";

        protected override Task<LLMResponse> SendCoreAsync(LLMRequest request, CancellationToken cancellationToken)
        {
            // TODO: Map request -> OpenAI payload (Responses API recommended)
            // TODO: Send HTTP request
            // TODO: Parse response (text + tool calls)
            return Task.FromResult(new LLMResponse
            {
                succeeded = false,
                errorMessage = "OpenAIClient not implemented yet (SendCoreAsync)."
            });
        }
    }
}