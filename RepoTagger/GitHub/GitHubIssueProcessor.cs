using RepoTagger.AI;
using RepoTagger.AI.Domain;
using RepoTagger.Data;

using RepoTagger.GitHub.Dtos;
using System.Text.Json;



namespace RepoTagger.GitHub
{
    public class GitHubIssueProcessor 
    
    {
        private readonly IssueAnalysisPipeline _analysisPipeline;
        private readonly ILogger<GitHubIssueProcessor>  _logger;
        private readonly IGitHubIssueClient _githubClient;
        private readonly IssueDecisionRepository _issueDecisionRepository;
        private readonly IDuplicateDetector _duplicateDetector;
        private readonly RepositoryRateLimiter _rateLimiter;


        public GitHubIssueProcessor(IssueAnalysisPipeline analysisPipeline, IDuplicateDetector duplicateDetector, ILogger<GitHubIssueProcessor> logger, IGitHubIssueClient githubClient, IssueDecisionRepository issueDecisionRepository,RepositoryRateLimiter rateLimiter )
        {
            _analysisPipeline = analysisPipeline ?? throw new ArgumentNullException(nameof(analysisPipeline));
            _logger = logger  ?? throw new ArgumentNullException(nameof(logger));
            _githubClient = githubClient  ?? throw new ArgumentNullException(nameof(githubClient));
            _issueDecisionRepository = issueDecisionRepository ?? throw new ArgumentNullException(nameof(issueDecisionRepository));
            _duplicateDetector = duplicateDetector ?? throw new ArgumentNullException(nameof(duplicateDetector));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));


        }

        public sealed record ProcessJobOutcome(DateTime? DeferUntilUtc = null);
        public async Task<ProcessJobOutcome> ProcessJob(BackgroundJobDto job, CancellationToken ct)
        {
            var payload = JsonSerializer.Deserialize<GitHubIssueWebhookPayload>(job.Payload);
            if (payload == null)
            {
                throw new InvalidOperationException("Invalid payload.");
            }
            if (payload.Repository == null || string.IsNullOrEmpty(payload.Repository.FullName))
            {
                throw new InvalidOperationException("Repository missing.");
            }
            if (payload.Issue == null)
            {
                throw new InvalidOperationException("Issue missing.");
            }
            if (payload.Issue.Number == 0)
            {
                throw new InvalidOperationException("Issue number missing.");
            }

            var repository = payload.Repository.FullName;
            var issueNumber = payload.Issue.Number;

            // Only ever written once everything below marked load-bearing has actually
            // succeeded — treat this as "core obligations were met", not "we reached the end".

            var rateCheck = await _rateLimiter.CheckAsync(repository, ct);
            if (!rateCheck.Allowed)
            {
                _logger.LogWarning("{Repository} is over its daily AI call limit — deferring until {RetryAfter}.", repository, rateCheck.RetryAfterUtc);
                return new ProcessJobOutcome(rateCheck.RetryAfterUtc);
            }

            if (await _issueDecisionRepository.ExistsAsync(repository, issueNumber, ct))
            {
                _logger.LogInformation("Issue {Repository}#{IssueNumber} already has a recorded decision — skipping reprocessing.", repository, issueNumber);
                return new ProcessJobOutcome();
            }



            var currentState = await _githubClient.GetIssueStateAsync(repository, issueNumber, ct);
            if (currentState != "open")
            {
                _logger.LogInformation("Issue {Repository}#{IssueNumber} is no longer open ({State}) — skipping stale processing.",
                    repository, issueNumber, currentState);
                return new ProcessJobOutcome();
            }



            var analysis = await _analysisPipeline.AnalyzeAsync(repository, payload.Issue, ct);



            if (analysis.Duplicates.Any())
            {
                // Best-effort: supplementary info, not part of the bot's core contract.
                try
                {
                    var list = string.Join(", ", analysis.Duplicates.Select(d => $"#{d.IssueNumber} ({d.Similarity:P0})"));
                    await _githubClient.AddCommentAsync(repository, issueNumber,
                        $"This issue looks similar to: {list}. A maintainer should confirm before closing as duplicate.", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Duplicate-candidate comment failed");
                }
            }

            var result = analysis.Classification;
            _logger.LogInformation("Issue classified as {Label} ({Confidence})", result.Label, result.Confidence);

            try
            {
                await _githubClient.AddLabelAsync(repository, issueNumber, GitHubLabelMapper.ToGitHubLabel(result.Label), ct);
                _logger.LogInformation("Applied label {Label} to issue {Issue}", result.Label, issueNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Label failed");
                throw;
            }

            if (result.Label == "needs-human-review")
            {
                // Load-bearing: the only human-facing explanation of *why* review is
                // needed. A failure here means the job genuinely isn't done — retry it.
                try
                {
                    var comment = $"This issue needs human review (confidence: {result.Confidence:P0}). {result.Reason}\n\n{GitHubComments.NeedsHumanReview}";
                    await _githubClient.AddCommentAsync(repository, issueNumber, comment, ct);
                    _logger.LogInformation("comment added");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Needs-human-review comment failed");
                    throw;
                }
            }

            await _duplicateDetector.StoreAsync(repository, issueNumber, analysis.RedactedTitle, analysis.RedactedBody, ct);

            await _issueDecisionRepository.AddAsync(new IssueDecision
            {
                Repository = repository,
                IssueNumber = issueNumber,
                Label = result.Label,
                Confidence = result.Confidence,
                ModelReason = result.Reason,
                DecidedAtUtc = analysis.Metadata.PerformedAtUtc,
                Provider = analysis.Metadata.Provider,
                Model = analysis.Metadata.Model,
            }, ct);

            return new ProcessJobOutcome();
        }

    }
}
