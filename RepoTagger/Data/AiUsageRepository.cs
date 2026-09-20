using Dapper;

namespace RepoTagger.Data
{
    public sealed class AiUsageRepository
    {
        private readonly DataContext _context;

        public AiUsageRepository(DataContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }


        public Task RecordAsync(
    string repository, int issueNumber, string callType, string provider, string model,
    long? inputTokens, long? outputTokens, long? reasoningTokens, long? totalTokens, CancellationToken ct)
    => RecordAsync(repository, issueNumber, callType, provider, model, inputTokens, outputTokens, reasoningTokens, totalTokens, ct, DateTime.UtcNow);

        internal async Task RecordAsync(
    string repository, int issueNumber, string callType, string provider, string model,
    long? inputTokens, long? outputTokens, long? reasoningTokens, long? totalTokens, CancellationToken ct, DateTime occurredAtUtc)
        {
            using var connection = _context.CreateConnection();
            const string sql = """
                INSERT INTO AiUsageEvents
                (Repository, IssueNumber, CallType, Provider, Model, InputTokens, OutputTokens, ReasoningTokens, TotalTokens, OccurredAtUtc)
                VALUES
                (@Repository, @IssueNumber, @CallType, @Provider, @Model, @InputTokens, @OutputTokens, @ReasoningTokens, @TotalTokens, @OccurredAtUtc);
                """;
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                Repository = repository,
                IssueNumber = issueNumber,
                CallType = callType,
                Provider = provider,
                Model = model,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                ReasoningTokens = reasoningTokens,
                TotalTokens = totalTokens,
                OccurredAtUtc = occurredAtUtc

            }, cancellationToken: ct));
        }




        public sealed record CostReportRow
        {
            public string Repository { get; init; } = "";
            public string Provider { get; init; } = "";
            public string Model { get; init; } = "";
            public long TotalCalls { get; init; }
            public long CallsWithKnownTokens { get; init; }
            public long? SumOfKnownTokens { get; init; }
            public long CallsWithKnownCost { get; init; }
            public double? SumOfKnownCostUsd { get; init; }
        }

        public async Task<IReadOnlyList<CostReportRow>> GetCostReportAsync(CancellationToken ct)    
        {
            using var connection = _context.CreateConnection();
            const string sql = """
        WITH Priced AS (
            SELECT
                u.Repository, u.Provider, u.Model, u.InputTokens, u.OutputTokens, u.ReasoningTokens, u.TotalTokens,
                (SELECT p.InputPricePerMillionTokens FROM AiPricing p
                 WHERE p.Provider = u.Provider AND p.Model = u.Model AND p.EffectiveFromUtc <= u.OccurredAtUtc
                 ORDER BY p.EffectiveFromUtc DESC LIMIT 1) AS InputPrice,
                (SELECT p.OutputPricePerMillionTokens FROM AiPricing p
                 WHERE p.Provider = u.Provider AND p.Model = u.Model AND p.EffectiveFromUtc <= u.OccurredAtUtc
                 ORDER BY p.EffectiveFromUtc DESC LIMIT 1) AS OutputPrice
            FROM AiUsageEvents u
        )
        SELECT
            Repository, Provider, Model,
            COUNT(*) AS TotalCalls,
            COUNT(TotalTokens) AS CallsWithKnownTokens,
            SUM(TotalTokens) AS SumOfKnownTokens,
            COUNT(CASE WHEN TotalTokens IS NOT NULL AND InputPrice IS NOT NULL AND OutputPrice IS NOT NULL THEN 1 END) AS CallsWithKnownCost,
            SUM(
                CASE WHEN TotalTokens IS NOT NULL AND InputPrice IS NOT NULL AND OutputPrice IS NOT NULL THEN
                    (COALESCE(InputTokens,0) / 1000000.0) * InputPrice +
                    ((COALESCE(OutputTokens,0) + COALESCE(ReasoningTokens,0)) / 1000000.0) * OutputPrice
                END
            ) AS SumOfKnownCostUsd
        FROM Priced
        GROUP BY Repository, Provider, Model;
        """;
            return (await connection.QueryAsync<CostReportRow>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
        }

        public sealed record UsageEventDetail
        {
            public string Repository { get; init; } = "";
            public long IssueNumber { get; init; }
            public string CallType { get; init; } = "";
            public string Provider { get; init; } = "";
            public string Model { get; init; } = "";
            public long? TotalTokens { get; init; }
            public double? CostUsd { get; init; }
            public DateTime OccurredAtUtc { get; init; }
        }
        public async Task<IReadOnlyList<UsageEventDetail>> GetUsageDetailAsync(bool onlyUnknownCost, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            const string sql = """
        WITH Priced AS (
            SELECT u.Repository, u.IssueNumber, u.CallType, u.Provider, u.Model,
                   u.InputTokens, u.OutputTokens, u.ReasoningTokens, u.TotalTokens, u.OccurredAtUtc,
                   (SELECT p.InputPricePerMillionTokens FROM AiPricing p
                    WHERE p.Provider = u.Provider AND p.Model = u.Model AND p.EffectiveFromUtc <= u.OccurredAtUtc
                    ORDER BY p.EffectiveFromUtc DESC LIMIT 1) AS InputPrice,
                   (SELECT p.OutputPricePerMillionTokens FROM AiPricing p
                    WHERE p.Provider = u.Provider AND p.Model = u.Model AND p.EffectiveFromUtc <= u.OccurredAtUtc
                    ORDER BY p.EffectiveFromUtc DESC LIMIT 1) AS OutputPrice
            FROM AiUsageEvents u
        )
        SELECT Repository, IssueNumber, CallType, Provider, Model, TotalTokens, OccurredAtUtc,
               CASE WHEN TotalTokens IS NOT NULL AND InputPrice IS NOT NULL AND OutputPrice IS NOT NULL THEN
                   (COALESCE(InputTokens,0) / 1000000.0) * InputPrice +
                   ((COALESCE(OutputTokens,0) + COALESCE(ReasoningTokens,0)) / 1000000.0) * OutputPrice
               END AS CostUsd
        FROM Priced
        WHERE (@OnlyUnknownCost = 0) OR (TotalTokens IS NULL OR InputPrice IS NULL OR OutputPrice IS NULL)
        ORDER BY OccurredAtUtc DESC;
        """;
            return (await connection.QueryAsync<UsageEventDetail>(
                new CommandDefinition(sql, new { OnlyUnknownCost = onlyUnknownCost }, cancellationToken: ct))).ToList();
        }

        public async Task<DateTime?> GetOldestCallInWindowAsync(string repository, DateTime sinceUtc, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            const string sql = "SELECT MIN(OccurredAtUtc) FROM AiUsageEvents WHERE Repository = @Repository AND OccurredAtUtc >= @SinceUtc;";
            return await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(sql, new { Repository = repository, SinceUtc = sinceUtc }, cancellationToken: ct));
        }


        public async Task<int> CountCallsSinceAsync(string repository, DateTime sinceUtc, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            const string sql = "SELECT COUNT(*) FROM AiUsageEvents WHERE Repository = @Repository AND OccurredAtUtc >= @SinceUtc;";
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Repository = repository, SinceUtc = sinceUtc }, cancellationToken: ct));
        }
    }
}