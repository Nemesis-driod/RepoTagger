using Microsoft.SemanticKernel;

namespace RepoTagger.AI.Providers
{
    public class OpenAICompatibleKernelProvider : IKernelProvider
    {


        public bool CanHandle(string provider)
        {

            return provider.Equals( "Groq",StringComparison.OrdinalIgnoreCase) ||
                   provider.Equals(
                        "DeepSeek",StringComparison.OrdinalIgnoreCase) ||
                   provider.Equals(  "Mistral", StringComparison.OrdinalIgnoreCase)
                || provider.Equals(
                        "OpenRouter", StringComparison.OrdinalIgnoreCase);

        }



        public void Configure(
            IKernelBuilder builder,
            AIOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Endpoint))
            {
                throw new InvalidOperationException("Endpoint missing");
            }


            builder.AddOpenAIChatCompletion(
                modelId: options.ModelId,endpoint: new Uri(options.Endpoint),apiKey: options.ApiKey);
        }

    }
}
