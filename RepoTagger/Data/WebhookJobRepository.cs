using Dapper;
using RepoTagger.GitHub.Dtos;
using static RepoTagger.Data.Metrics;


namespace RepoTagger.Data
{
    public class WebhookJobRepository
    {
        private readonly DataContext _context;

        public WebhookJobRepository( DataContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }


        public async Task MarkCompleted(long id)
        {
            using var connection = _context.CreateConnection();
            string sql = "UPDATE WebhookJobs SET Status = 'Completed', CompletedAtUtc = @CompletedAtUtc WHERE Id = @Id;";

            await connection.ExecuteAsync(  sql,  new{
                Id = id,
                CompletedAtUtc = DateTime.UtcNow
            });

        }

        public async Task MarkFailed(long id , string error)
        {
            using var connection = _context.CreateConnection();
            string sql = "UPDATE WebhookJobs SET Status = 'Failed', LastError = @LastError WHERE Id = @Id;";

            await connection.ExecuteAsync(sql, new
            {
                Id = id,
                LastError = error
            });

        }

        public async Task MarkPending(long id , string error, int attempt)
        {
            using var connection = _context.CreateConnection();

            var delay = attempt switch
            {
                1 => TimeSpan.FromSeconds(30),
                2 => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromMinutes(30)
            };

            string sql = "Update WebhookJobs set Status= 'Pending',StartedAtUtc=NULL, NextAttemptAtUtc=@NextAttempt , LastError = @LastError where Id = @Id;";


            var nextAttempt = DateTime.UtcNow + delay;
            await connection.ExecuteAsync(sql, new
            {
                Id = id,
                LastError = error,
              NextAttempt = nextAttempt
            });
        }

        public async Task<BackgroundJobDto?> ClaimNextJob()
        {
            using var connection = _context.CreateConnection();
            const string sql = """
        Update WebhookJobs   Set Status = 'Processing', StartedAtUtc = @Now, Attempts = Attempts + 1
        Where Id = (
            SELECT Id FROM WebhookJobs
            WHERE (Status = 'Pending' And (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= @Now))
               OR (Status = 'Processing' AND StartedAtUtc <= @StuckThreshold AND Attempts < 3)
            ORDER BY CreatedAtUtc LIMIT 1
        )
        RETURNING *;
        """;
            return await connection.QueryFirstOrDefaultAsync<BackgroundJobDto>(sql, new
            {
                Now = DateTime.UtcNow,
                StuckThreshold = DateTime.UtcNow - TimeSpan.FromMinutes(10)
            });
        }
        public async Task<bool> TryEnqueueAsync( string deliveryId,BackgroundJobDto job)
        {
            using var connection = _context.CreateConnection();

            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                const string deliverySql = """
        INSERT OR IGNORE INTO WebhookDeliveries
        ( DeliveryId, ReceivedAtUtc) VALUES ( @DeliveryId, @ReceivedAtUtc );
        """;


                var inserted = await connection.ExecuteAsync(
                    deliverySql, new{
                        DeliveryId = deliveryId,
                        ReceivedAtUtc = DateTime.UtcNow
                    },transaction);


                if (inserted == 0)
                {
                    transaction.Rollback();
                    return false;
                }



                const string jobSql = """
    INSERT INTO WebhookJobs
    (DeliveryId, EventType, Action, Payload, Status, Attempts, CreatedAtUtc, StartedAtUtc, CompletedAtUtc, LastError)
    VALUES (@DeliveryId, @EventType, @Action, @Payload, @Status, @Attempts, @CreatedAtUtc, @StartedAtUtc, @CompletedAtUtc, @LastError);
    """;


                await connection.ExecuteAsync(jobSql, job, transaction);
                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }



        public async Task<OperationsReport> GetOperationsReportAsync(CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            const string sql = """
        SELECT
            (SELECT COUNT(*) FROM WebhookJobs WHERE Status = 'Pending') AS PendingJobs,
            (SELECT COUNT(*) FROM WebhookJobs WHERE Status = 'Failed') AS FailedJobs,
            (SELECT COUNT(*) FROM WebhookJobs WHERE Status = 'Processing' AND StartedAtUtc <= @StuckThreshold) AS StuckJobs,
            (SELECT COUNT(*) FROM WebhookDeliveries) AS TotalWebhookDeliveries,
            (SELECT CAST(SUM(CASE WHEN Attempts > 1 THEN 1 ELSE 0 END) AS REAL) / NULLIF(COUNT(*), 0) FROM WebhookJobs) AS JobRetryRate,
            (SELECT AVG((julianday(CompletedAtUtc) - julianday(CreatedAtUtc)) * 86400.0)
                FROM WebhookJobs WHERE Status = 'Completed') AS AverageProcessingTimeSeconds;
        """;
            return await connection.QuerySingleAsync<OperationsReport>(new CommandDefinition(
                sql, new { StuckThreshold = DateTime.UtcNow - TimeSpan.FromMinutes(10) }, cancellationToken: ct));
        }


        public async Task DeferAsync(long id, TimeSpan delay, string reason)
        {
            using var connection = _context.CreateConnection();
            const string sql = "UPDATE WebhookJobs SET Status = 'Pending', StartedAtUtc = NULL, NextAttemptAtUtc = @NextAttempt, LastError = @Reason WHERE Id = @Id;";
            await connection.ExecuteAsync(sql, new { Id = id, NextAttempt = DateTime.UtcNow + delay, Reason = reason });
        }
    }
}





