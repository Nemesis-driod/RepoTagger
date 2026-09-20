using Microsoft.SemanticKernel;


namespace RepoTagger.AI.Providers
{
    public  sealed class GeminiKernelProvider : IKernelProvider
    {

        public const string PrimaryServiceId = "primary";
        public const string EscalationServiceId = "escalation";

        public bool CanHandle(string provider)
        {
            return provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase);
        }

        public void Configure(IKernelBuilder builder, AIOptions options)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrWhiteSpace(options.ApiKey))
                throw new InvalidOperationException("Gemini API key missing");
            if (string.IsNullOrWhiteSpace(options.ModelId))
                throw new InvalidOperationException("Gemini chat model ID missing.");


#pragma warning disable SKEXP0070
            builder.AddGoogleAIGeminiChatCompletion(modelId: options.ModelId, apiKey: options.ApiKey, serviceId: PrimaryServiceId);

            if (!string.IsNullOrWhiteSpace(options.EscalationModelId))
            {
                builder.AddGoogleAIGeminiChatCompletion(modelId: options.EscalationModelId, apiKey: options.ApiKey, serviceId: EscalationServiceId);
            }

            if (!string.IsNullOrWhiteSpace(options.EmbeddingModelId))
            {
                builder.AddGoogleAIEmbeddingGenerator(modelId: options.EmbeddingModelId, apiKey: options.ApiKey);
            }
#pragma warning restore SKEXP0070
        }
    }
}
