using Dapper;
using RepoTagger.Data;

public sealed class AiPricingRepository
{
    private readonly DataContext _context;
    public AiPricingRepository(DataContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task AddPriceAsync(string provider, string model,
        double inputPricePerMillion, double outputPricePerMillion, DateTime effectiveFromUtc, CancellationToken ct)
    {
        using var connection = _context.CreateConnection();
        const string sql = """
            INSERT INTO AiPricing (Provider, Model, InputPricePerMillionTokens, OutputPricePerMillionTokens, EffectiveFromUtc, RecordedAtUtc)
            VALUES (@Provider, @Model, @InputPrice, @OutputPrice, @EffectiveFromUtc, @RecordedAtUtc);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Provider = provider,
            Model = model,
            InputPrice = inputPricePerMillion,
            OutputPrice = outputPricePerMillion,
            EffectiveFromUtc = effectiveFromUtc,
            RecordedAtUtc = DateTime.UtcNow
        }, cancellationToken: ct));
    }
}