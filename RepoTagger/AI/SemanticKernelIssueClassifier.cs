
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using RepoTagger.AI.Domain;
using RepoTagger.AI.Prompts;
using RepoTagger.AI.Providers;
using RepoTagger.Data;
using System.Text.Json;

namespace RepoTagger.AI
{
    public class SemanticKernelIssueClassifier : IIssueClassifier
    {
        private readonly Kernel _kernel;
        private readonly KernelFunction _classifierFunction;
        private readonly ConfidenceGate _gate;
        private readonly AiUsageRepository _aiUsageRepository;
        private readonly AIOptions _options;
        private readonly ILogger<SemanticKernelIssueClassifier> _logger;

        public SemanticKernelIssueClassifier(
            Kernel kernel, PromptLoader promptLoader, ConfidenceGate gate,
            AiUsageRepository aiUsageRepository, IOptions<AIOptions> options, ILogger<SemanticKernelIssueClassifier> logger)
        {
            ArgumentNullException.ThrowIfNull(kernel);
            ArgumentNullException.ThrowIfNull(promptLoader);
            ArgumentNullException.ThrowIfNull(gate);
            ArgumentNullException.ThrowIfNull(aiUsageRepository);
            ArgumentNullException.ThrowIfNull(logger);

            _kernel = kernel;
            _gate = gate;
            _aiUsageRepository = aiUsageRepository;
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
            _classifierFunction = kernel.CreateFunctionFromPromptYaml(promptLoader.Load("IssueClassifier.yaml"));
        }

        internal sealed record AiClassificationResponse(string? Label, double? Confidence, string? Reason);

        internal sealed record AiCallUsage(long? InputTokens, long? OutputTokens, long? ReasoningTokens, long? TotalTokens);

        private sealed record EvaluationResult(IssueClassificationResult Decision, bool ShouldEscalate);


        public async Task<IssueClassificationResult> ClassifyAsync(IssueClassificationRequest request, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var primaryResponse = await InvokeAsync(request, GeminiKernelProvider.PrimaryServiceId, _options.ModelId, "Classification", ct);
            var primaryEvaluation = Evaluate(primaryResponse);

            if (!primaryEvaluation.ShouldEscalate)
            {
                return primaryEvaluation.Decision;
            }

            _logger.LogInformation("Primary classification for {Repository}#{IssueNumber} was low-confidence — escalating to {Model}.",
                request.Repository, request.IssueNumber, _options.EscalationModelId);

            try
            {
                var escalatedResponse = await InvokeAsync(request, GeminiKernelProvider.EscalationServiceId, _options.EscalationModelId!, "ClassificationEscalation", ct);
                return Evaluate(escalatedResponse).Decision;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Escalation failed for {Repository}#{IssueNumber} — falling back to the primary result.",
                    request.Repository, request.IssueNumber);
                return primaryEvaluation.Decision;
            }
        }
        private async Task<AiClassificationResponse> InvokeAsync(
            IssueClassificationRequest request, string serviceId, string modelId, string callType, CancellationToken ct)
        {
            var arguments = new KernelArguments(new PromptExecutionSettings { ServiceId = serviceId })
            {
                ["title"] = request.Title,
                ["body"] = request.Body
            };

            FunctionResult result;
            try
            {
                result = await _kernel.InvokeAsync(_classifierFunction, arguments, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                await TryRecordUsageAsync(request, callType, modelId, null, ct);
                throw new InvalidOperationException("AI classification failed.", ex);
            }

            var rawResponse = result.GetValue<string>();
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                await TryRecordUsageAsync(request, callType, modelId, null, ct);
                throw new InvalidOperationException("AI returned empty response.");
            }

            await TryRecordUsageAsync(request, callType, modelId, ExtractUsage(result), ct);
            return DeserializeResponse(rawResponse);
        }

        private async Task TryRecordUsageAsync(IssueClassificationRequest request, string callType, string modelId, AiCallUsage? usage, CancellationToken ct)
        {
            try
            {
                await _aiUsageRepository.RecordAsync(
                    request.Repository, request.IssueNumber, callType, _options.Provider, modelId,
                    usage?.InputTokens, usage?.OutputTokens, usage?.ReasoningTokens, usage?.TotalTokens, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record AI usage for {Repository}#{IssueNumber} — continuing without it.", request.Repository, request.IssueNumber);
            }
        }

        //private IssueClassificationResult Evaluate(AiClassificationResponse response)
        //{
        //    var normalized = Normalize(response);

        //    if (!AllowedActions.IsAllowedLabel(normalized.Label))
        //    {
        //        return new IssueClassificationResult("needs-human-review", normalized.Confidence, normalized.Reason);
        //    }

        //    if (_gate.Evaluate(normalized.Confidence) == TaggerOutcome.NeedsHumanReview)
        //    {
        //        return new IssueClassificationResult("needs-human-review", normalized.Confidence, normalized.Reason);
        //    }

        //    _logger.LogInformation("Issue classified. Label={Label}, Confidence={Confidence}", normalized.Label, normalized.Confidence);
        //    return normalized;
        //}
   
        private AiCallUsage ExtractUsage(FunctionResult result)
        {
            if (result.Metadata == null)
            {
                _logger.LogWarning("Classification succeeded but no metadata was returned — recording this call with unknown token cost.");
                return new AiCallUsage(null, null, null, null);
            }

            return new AiCallUsage(
                ReadTokenCount(result.Metadata, "PromptTokenCount"),
                ReadTokenCount(result.Metadata, "CandidatesTokenCount"),
                ReadTokenCount(result.Metadata, "ThoughtsTokenCount"),
                ReadTokenCount(result.Metadata, "TotalTokenCount"));
        }

        private static long? ReadTokenCount(IReadOnlyDictionary<string, object?> metadata, string key)
        {
            if (!metadata.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }
            return value switch
            {
                long l => l,
                int i => i,
                _ => Convert.ToInt64(value)
            };
        }

        private EvaluationResult Evaluate(AiClassificationResponse response)
        {
            var normalized = Normalize(response);
            var labelAllowed = AllowedActions.IsAllowedLabel(normalized.Label);
            var gateOutcome = labelAllowed ? _gate.Evaluate(normalized.Confidence) : TaggerOutcome.NeedsHumanReview;
            var shouldEscalate = ClassificationEscalationPolicy.ShouldEscalate(gateOutcome, labelAllowed, _options.EscalationModelId);

            if (gateOutcome == TaggerOutcome.NeedsHumanReview)
            {
                return new EvaluationResult(new IssueClassificationResult("needs-human-review", normalized.Confidence, normalized.Reason), shouldEscalate);
            }

            _logger.LogInformation("Issue classified. Label={Label}, Confidence={Confidence}", normalized.Label, normalized.Confidence);
            return new EvaluationResult(normalized, false);
        }

        private static AiClassificationResponse DeserializeResponse(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<AiClassificationResponse>(
                           json, new JsonSerializerOptions
                           {
                               PropertyNameCaseInsensitive = true
                           }) ?? throw new InvalidOperationException("AI response was null.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("AI returned invalid JSON.", ex);
            }
        }


        private static IssueClassificationResult Normalize(AiClassificationResponse response)
        {
            var label = response.Label?
                    .Trim().ToLowerInvariant() ?? string.Empty;


            var confidence =
                response.Confidence.HasValue ? Math.Clamp(
                        response.Confidence.Value, 0, 1) : 0;


            var reason = response.Reason?.Trim() ?? string.Empty;



            return new IssueClassificationResult(
                label, confidence, reason);
        }


    }

}




