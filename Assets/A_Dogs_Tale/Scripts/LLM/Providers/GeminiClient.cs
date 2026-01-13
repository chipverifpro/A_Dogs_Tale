using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;

namespace DogGame.LLM.Providers
{
    public sealed class GeminiClient : LLMClientBase
    {
        public override string Vendor => "Gemini";

        protected override Task<LLMResponse> SendCoreAsync(LLMRequest request, CancellationToken cancellationToken)
        {
            // TODO: Map request -> Gemini payload
            // TODO: Send HTTP request
            // TODO: Parse response (text + tool calls)
            return Task.FromResult(new LLMResponse
            {
                succeeded = false,
                errorMessage = "GeminiClient not implemented yet (SendCoreAsync)."
            });
        }
    }
}