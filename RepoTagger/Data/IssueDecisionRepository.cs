using Dapper;
using static RepoTagger.Data.Metrics;

namespace RepoTagger.Data
{
    public class IssueDecisionRepository
    {
        private readonly DataContext _context;

        public IssueDecisionRepository(DataContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<bool> ExistsAsync(string repository, int issueNumber, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            const string sql = """
                SELECT EXISTS(
                    SELECT 1 FROM IssueDecisions WHERE Repository = @Repository AND IssueNumber = @IssueNumber
                );
                """;
            return await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(sql, new { Repository = repository, IssueNumber = issueNumber }, cancellationToken: ct));
        }

        public async Task AddAsync(IssueDecision decision, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            const string sql = """
                INSERT INTO IssueDecisions (Repository, IssueNumber, Label, Confidence, ModelReason, DecidedAtUtc, Model, Provider)
                VALUES (@Repository, @IssueNumber, @Label, @Confidence, @ModelReason, @DecidedAtUtc, @Model, @Provider)
                ON CONFLICT (Repository, IssueNumber) DO UPDATE SET
                    Label = excluded.Label,
                    Confidence = excluded.Confidence,
                    ModelReason = excluded.ModelReason,
                    DecidedAtUtc = excluded.DecidedAtUtc,
                    Model = excluded.Model,
                    Provider = excluded.Provider;
                """;
            await connection.ExecuteAsync(new CommandDefinition(sql, decision, cancellationToken: ct));
        }


        public async Task RecordLabelRemovalAsync(string repository, int issueNumber, string removedLabel, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            const string sql = """
        INSERT OR IGNORE INTO LabelOverrideEvents (Repository, IssueNumber, RemovedLabel, ObservedAtUtc)
        VALUES (@Repository, @IssueNumber, @RemovedLabel, @ObservedAtUtc);
        """;
            await connection.ExecuteAsync(new CommandDefinition(sql,
                new { Repository = repository, IssueNumber = issueNumber, RemovedLabel = removedLabel, ObservedAtUtc = DateTime.UtcNow },
                cancellationToken: ct));
        }


        // reference code
        public async Task<IReadOnlyList<LabelAccuracyReport>> GetAccuracyReportAsync(CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            const string sql = """
        SELECT
            d.Label AS Label,
            COUNT(*) AS TotalDecisions,
            SUM(CASE WHEN o.Id IS NOT NULL THEN 1 ELSE 0 END) AS OverriddenCount,
            AVG(d.Confidence) AS AverageConfidence,
            AVG(
                CASE WHEN o.ObservedAtUtc IS NOT NULL
                THEN (julianday(o.ObservedAtUtc) - julianday(d.DecidedAtUtc)) * 24.0
                ELSE NULL END
            ) AS AverageHoursToOverride
        FROM IssueDecisions d
        LEFT JOIN LabelOverrideEvents o
            ON o.Repository = d.Repository AND o.IssueNumber = d.IssueNumber
            AND o.RemovedLabel = 'ai:' || d.Label
        GROUP BY d.Label;
        """;
            return (await connection.QueryAsync<LabelAccuracyReport>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
        }


        public async Task<IReadOnlyList<IssueBreakdown>> GetIssueBreakdownAsync(CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            const string sql = """
        SELECT Repository, Label, Provider, Model,
               COUNT(*) AS Count,
               AVG(Confidence) AS AverageConfidence,
               SUM(CASE WHEN Confidence < 0.90 THEN 1 ELSE 0 END) AS LowConfidenceCount
        FROM IssueDecisions
        GROUP BY Repository, Label, Provider, Model;
        """;
            return (await connection.QueryAsync<IssueBreakdown>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
        }
    }
}