using RepoTagger.Data;
using RepoTagger.GitHub.Dtos;
using System.Text.Json;

namespace RepoTagger.GitHub
{
    public class IssueReconciliationProcessor
    {
        private readonly IssueDecisionRepository _issueDecisionRepository;
        private readonly ILogger<IssueReconciliationProcessor> _logger;


        public IssueReconciliationProcessor(IssueDecisionRepository issueDecisionRepository, ILogger<IssueReconciliationProcessor> logger)
        {
            _issueDecisionRepository = issueDecisionRepository ?? throw new ArgumentNullException(nameof(issueDecisionRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public async Task ProcessJob(BackgroundJobDto job, CancellationToken ct)
        {
            var payload = JsonSerializer.Deserialize<GitHubIssueWebhookPayload>(job.Payload);
            if (payload == null) throw new InvalidOperationException("Invalid payload.");
            if (payload.Repository == null || string.IsNullOrEmpty(payload.Repository.FullName))
                throw new InvalidOperationException("Repository missing.");
            if (payload.Issue == null || payload.Issue.Number == 0)
                throw new InvalidOperationException("Issue missing.");

            if (payload.Label == null || !payload.Label.Name.StartsWith("ai:", StringComparison.Ordinal))
            {
                _logger.LogInformation("Ignoring non-ai label removal on {Repository}#{IssueNumber}.",
                    payload.Repository.FullName, payload.Issue.Number);
                return;
            }

            await _issueDecisionRepository.RecordLabelRemovalAsync(
                payload.Repository.FullName, payload.Issue.Number, payload.Label.Name, ct);
        }
    }
}
