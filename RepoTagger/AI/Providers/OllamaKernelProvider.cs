using Microsoft.SemanticKernel;

namespace RepoTagger.AI.Providers
{
    public sealed class OllamaKernelProvider : IKernelProvider
    {
        public bool CanHandle(string provider)
        {
            return provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase);
        }

        public void Configure(IKernelBuilder builder, AIOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Endpoint))
                throw new InvalidOperationException(
                    "Ollama endpoint missing");


            builder.AddOllamaChatCompletion(modelId: options.ModelId, endpoint: new Uri(options.Endpoint));
        }
    }
}
