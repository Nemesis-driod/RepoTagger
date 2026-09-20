using Microsoft.SemanticKernel;
using RepoTagger.AI.Providers;

namespace RepoTagger.AI
{
    public  static class KernelServiceCollectionExtensions
    {
        public static IServiceCollection AddRepoTaggerKernel(this IServiceCollection services, IConfiguration configuration)
        {
           var options =  configuration.GetSection("AI").Get<AIOptions>() ?? throw new InvalidOperationException(
                "AI configuration missing");


            if (string.IsNullOrWhiteSpace(options.Provider))
                throw new InvalidOperationException("AI Provider missing");

            if (string.IsNullOrWhiteSpace(options.ModelId))
            {
                throw new InvalidOperationException("AI Model ID missing");
            }


            if (options.MaxAiCallsPerDayPerRepository <= 0)
            {
                throw new InvalidOperationException(
                    "AI:MaxAiCallsPerDayPerRepository must be greater than zero.");
            }



            var kernalBuilder = services.AddKernel();

            IKernelProvider[] providers = [
            new GeminiKernelProvider(),
            new OllamaKernelProvider(),
            new OpenAICompatibleKernelProvider()
        ];
            var provider = providers.FirstOrDefault(p => p.CanHandle(options.Provider))
    ?? throw new InvalidOperationException($"No kernel provider registered for AI:Provider '{options.Provider}'.");

            provider.Configure(kernalBuilder, options);

            return services;

        }
    }
}
