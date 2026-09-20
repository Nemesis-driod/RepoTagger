using RepoTagger.Data;
using RepoTagger.GitHub;
using RepoTagger.GitHub.Dtos;
using System.Text.Json;

namespace RepoTagger.Background
{
    public class BackgroundServices : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundServices> _logger;

        public BackgroundServices(IServiceScopeFactory serviceScopeFactory, ILogger<BackgroundServices> logger)
        {
            _scopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<WebhookJobRepository>();

                try
                {
                    var job = await repository.ClaimNextJob();
                    if (job == null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        continue;
                    }

                    try
                    {
                        if (job.Action == "unlabeled")
                        {
                            var reconciliationProcessor = scope.ServiceProvider.GetRequiredService<IssueReconciliationProcessor>();
                            await reconciliationProcessor.ProcessJob(job, stoppingToken);
                            await repository.MarkCompleted(job.Id);
                        }
                        else if (job.Action == "opened")
                        {
                            var issueProcessor = scope.ServiceProvider.GetRequiredService<GitHubIssueProcessor>();
                            var outcome = await issueProcessor.ProcessJob(job, ct: stoppingToken);

                            if (outcome.DeferUntilUtc.HasValue)
                            {
                                var delay = outcome.DeferUntilUtc.Value - DateTime.UtcNow;
                                await repository.DeferAsync(job.Id, delay > TimeSpan.Zero ? delay : TimeSpan.FromMinutes(1), "Deferred: rate limit exceeded");
                            }
                            else
                            {
                                await repository.MarkCompleted(job.Id);
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unexpected job action '{job.Action}'.");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (job.Attempts < 3)
                            await repository.MarkPending(job.Id, ex.Message, job.Attempts);
                        else
                            await repository.MarkFailed(job.Id, ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background job loop iteration failed unexpectedly.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
    }
}