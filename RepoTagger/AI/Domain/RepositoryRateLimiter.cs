using Microsoft.Extensions.Options;
using RepoTagger.Data;

namespace RepoTagger.AI.Domain
{
    public sealed class RepositoryRateLimiter
    {
        private readonly AiUsageRepository _usageRepository;
        private readonly AIOptions _options;

        public RepositoryRateLimiter(AiUsageRepository usageRepository, IOptions<AIOptions> options)
        {
            _usageRepository = usageRepository ?? throw new ArgumentNullException(nameof(usageRepository));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public sealed record RateLimitCheckResult(bool Allowed, DateTime? RetryAfterUtc);

        public async Task<RateLimitCheckResult> CheckAsync(string repository, CancellationToken ct)
        {
            var windowStart = DateTime.UtcNow.AddDays(-1);
            var callCount = await _usageRepository.CountCallsSinceAsync(repository, windowStart, ct);

            if (callCount < _options.MaxAiCallsPerDayPerRepository)
            {
                return new RateLimitCheckResult(true, null);
            }

            var oldestInWindow = await _usageRepository.GetOldestCallInWindowAsync(repository, windowStart, ct);
            var retryAfter = oldestInWindow.HasValue
                ? oldestInWindow.Value.AddDays(1).AddMinutes(1)
                : DateTime.UtcNow.AddMinutes(15);

            return new RateLimitCheckResult(false, retryAfter);
        }
    }
}
