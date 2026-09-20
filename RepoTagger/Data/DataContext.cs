using Microsoft.Data.Sqlite;
using System.Data;

namespace RepoTagger.Data
{
    public class DataContext
    {
        private readonly IConfiguration _configuration;

        public DataContext(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }


        public string ConnectionString => _configuration.GetConnectionString("RepoTagger") ?? throw new InvalidOperationException("Connection String 'RepoTagger'  is not configured.");


        public IDbConnection CreateConnection()
        {
            return new SqliteConnection(ConnectionString);
        }
    }
}
