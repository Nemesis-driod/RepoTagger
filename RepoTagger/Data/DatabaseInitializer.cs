using Dapper;

using System.Data;

namespace RepoTagger.Data
{
    public class DatabaseInitializer
    {
        private readonly DataContext _context;
        private readonly IIssueEmbeddingRepository _issueEmbeddingRepository;

        public DatabaseInitializer(DataContext context, IIssueEmbeddingRepository issueEmbeddingRepository)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _issueEmbeddingRepository = issueEmbeddingRepository ?? throw new ArgumentNullException(nameof(issueEmbeddingRepository));
        }


        public async Task InitializeAsync(CancellationToken ct = default)
        {
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync("PRAGMA journal_mode=WAL;");

            await CreateWebhookDeliveryTable(connection);
            await CreateWebhookJobTable(connection);
            await CreateWebhookJobStatusIndex(connection);
            await CreateIssueDecisionTable(connection);

            await _issueEmbeddingRepository.EnsureCollectionExistsAsync(ct);

            await CreateLabelOverrideEventTable(connection);
            await CreateAiUsageEventTable(connection);
         
            await CreateAiPricingTable(connection);

        }


        private async Task CreateWebhookDeliveryTable(
            IDbConnection connection)
        {
            await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS WebhookDeliveries
            ( DeliveryId TEXT PRIMARY KEY, ReceivedAtUtc TEXT NOT NULL );
            """);
        }


        private async Task CreateWebhookJobTable(IDbConnection connection)
        {
            await connection.ExecuteAsync("""
        CREATE TABLE IF NOT EXISTS WebhookJobs
        (
        Id INTEGER PRIMARY KEY AUTOINCREMENT, DeliveryId TEXT NOT NULL, EventType TEXT NOT NULL,
        Action TEXT NOT NULL DEFAULT '', Payload TEXT NOT NULL, Status TEXT NOT NULL, Attempts INTEGER NOT NULL DEFAULT 0,
        CreatedAtUtc TEXT NOT NULL, StartedAtUtc TEXT NULL, CompletedAtUtc TEXT NULL,
        LastError TEXT NULL, NextAttemptAtUtc TEXT NULL,
                FOREIGN KEY (DeliveryId) REFERENCES WebhookDeliveries(DeliveryId));
        """);
        }


        private async Task CreateWebhookJobStatusIndex(IDbConnection connection)
        {
            await connection.ExecuteAsync("""
            CREATE INDEX IF NOT EXISTS IX_WebhookJobs_Status_CreatedAtUtc
            ON WebhookJobs(Status, CreatedAtUtc);
    """);
        }
        private async Task CreateIssueDecisionTable(IDbConnection connection)
        {
            await connection.ExecuteAsync("""
        CREATE TABLE IF NOT EXISTS IssueDecisions
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Repository TEXT NOT NULL,
            IssueNumber INTEGER NOT NULL,
            Label TEXT NOT NULL,
            Confidence REAL NOT NULL,
            ModelReason TEXT NOT NULL,
            DecidedAtUtc TEXT NOT NULL,
            Model TEXT NOT NULL,
            Provider TEXT NOT NULL,
            UNIQUE (Repository, IssueNumber)
        );
        """);
        }


        private async Task CreateLabelOverrideEventTable(IDbConnection connection)
        {
            await connection.ExecuteAsync("""
        CREATE TABLE IF NOT EXISTS LabelOverrideEvents
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Repository TEXT NOT NULL,
            IssueNumber INTEGER NOT NULL,
            RemovedLabel TEXT NOT NULL,
            ObservedAtUtc TEXT NOT NULL,
            UNIQUE (Repository, IssueNumber, RemovedLabel)
        );
        """);
        }


        private async Task CreateAiUsageEventTable(IDbConnection connection)
        {
            await connection.ExecuteAsync("""
        CREATE TABLE IF NOT EXISTS AiUsageEvents
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Repository TEXT NOT NULL,
            IssueNumber INTEGER NOT NULL,
            CallType TEXT NOT NULL,
            Provider TEXT NOT NULL,
            Model TEXT NOT NULL,
            InputTokens INTEGER NULL,
            OutputTokens INTEGER NULL,
            ReasoningTokens INTEGER NULL,
            TotalTokens INTEGER NULL,
            OccurredAtUtc TEXT NOT NULL
        );
        """);
        }

        private async Task CreateAiPricingTable(IDbConnection connection)
        {
            await connection.ExecuteAsync("""
        CREATE TABLE IF NOT EXISTS AiPricing
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Provider TEXT NOT NULL,
            Model TEXT NOT NULL,
            InputPricePerMillionTokens REAL NOT NULL,
            OutputPricePerMillionTokens REAL NOT NULL,
            EffectiveFromUtc TEXT NOT NULL,
            RecordedAtUtc TEXT NOT NULL
        );
        """);
        }
    }
}
