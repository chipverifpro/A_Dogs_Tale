using System.Threading;
using System.Threading.Tasks;

namespace DogGame.LLM.Core
{
    public interface ILLMClient
    {
        string Vendor { get; }
        Task<LLMResponse> SendAsync(LLMRequest request, CancellationToken cancellationToken);
    }
}