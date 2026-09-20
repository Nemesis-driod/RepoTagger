using Microsoft.SemanticKernel;

namespace RepoTagger.AI.Providers
{
    public interface IKernelProvider
    {
        bool CanHandle(string provider);

        void Configure( IKernelBuilder builder, AIOptions options);
    }
}
