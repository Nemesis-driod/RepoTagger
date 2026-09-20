using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using RepoTagger.Data;

namespace RepoTagger.Tests;

public sealed class TestDatabase : IDisposable
{
    public string DatabasePath { get; }
    public DataContext Context { get; }
    public IssueEmbeddingRepository EmbeddingRepository { get; }
    public AiUsageRepository AiUsageRepository { get; }


    private TestDatabase( string databasePath,DataContext context, IssueEmbeddingRepository embeddingRepository,
    AiUsageRepository aiUsageRepository)
    {
        DatabasePath = databasePath;
        Context = context;
        EmbeddingRepository = embeddingRepository;
        AiUsageRepository = aiUsageRepository;
    }


    public static async Task<TestDatabase> CreateAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"RepoTaggerTests-{Guid.NewGuid():N}.db");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:RepoTagger"] = $"Data Source={databasePath};Foreign Keys=True;Default Timeout=5;"
            })
            .Build();

        var context = new DataContext(configuration);
        var embeddingRepository = new IssueEmbeddingRepository(context);

        var aiUsageRepository = new AiUsageRepository(context);


        // Same schema, same vector collection, same order as real startup —
        // deliberately not duplicating DatabaseInitializer's SQL here.
        var initializer = new DatabaseInitializer(context, embeddingRepository);
        await initializer.InitializeAsync();

        return new TestDatabase(databasePath, context, embeddingRepository, aiUsageRepository);
    }

    public void Dispose()
    {
        EmbeddingRepository.Dispose(); // assumes last message's IDisposable fix
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var file = DatabasePath + suffix;
            if (File.Exists(file)) File.Delete(file);
        }
    }
}