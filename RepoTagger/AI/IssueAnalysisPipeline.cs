using Microsoft.Extensions.Options;
using RepoTagger.GitHub.Dtos;
using RepoTagger.Security.Redaction;
using System.Diagnostics;

namespace RepoTagger.AI
{
    public sealed class IssueAnalysisPipeline
    {
        private readonly SecretRedactor _redactor;
        private readonly IIssueClassifier _classifier;
        private readonly ILogger<IssueAnalysisPipeline> _logger;
        private readonly AIOptions _options;
        private readonly IDuplicateDetector _duplicates;

        public IssueAnalysisPipeline(
            SecretRedactor redactor, IIssueClassifier classifier, IDuplicateDetector duplicates,
            ILogger<IssueAnalysisPipeline> logger, IOptions<AIOptions> options)
        {
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _duplicates = duplicates ?? throw new ArgumentNullException(nameof(duplicates));
        }

        public async Task<IssueAnalysis> AnalyzeAsync(string repository, GitHubIssueDto issue, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(issue);
            var title = _redactor.Redact(issue.Title);
            var body = _redactor.Redact(issue.Body);
            _logger.LogInformation("Issue redacted. Title findings {Title}, Body findings {Body}", title.Findings.Count, body.Findings.Count);

            var duplicates = await _duplicates.FindAsync(repository, issue.Number, title.Text, body.Text, ct);

            var request = new IssueClassificationRequest(repository, issue.Number, title.Text, body.Text);
            var started = Stopwatch.StartNew();
            var classification = await _classifier.ClassifyAsync(request, ct);
            started.Stop();

            var redactions = title.Findings.Concat(body.Findings).ToList();
            return new IssueAnalysis(classification, redactions, duplicates, title.Text, body.Text,
                new ClassificationMetadata(_options.Provider, _options.ModelId, started.Elapsed, DateTime.UtcNow));
        }
    }
}