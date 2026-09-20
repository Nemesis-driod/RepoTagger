using Dapper;

namespace RepoTagger.Data
{
    public class WebhookDeliveryRepository
    {
        private readonly DataContext _context;

        public WebhookDeliveryRepository(DataContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }


        public async Task<bool> TryRecordDeliveryAsync(string deliveryId)
        {
            using var connection = _context.CreateConnection();

            const string sql = """
        INSERT OR IGNORE INTO WebhookDeliveries( DeliveryId,ReceivedAtUtc ) VALUES  (@DeliveryId, @ReceivedAtUtc );
        """;


            var rows = await connection.ExecuteAsync( sql, new
                {
                    DeliveryId = deliveryId,
                    ReceivedAtUtc = DateTime.UtcNow
                });


            return rows == 1;
        }




       
    }
}
