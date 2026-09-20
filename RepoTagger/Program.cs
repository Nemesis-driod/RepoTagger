
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.SemanticKernel;
using RepoTagger.AI;
using RepoTagger.AI.Domain;
using RepoTagger.AI.Prompts;
using RepoTagger.Background;
using RepoTagger.Data;
using RepoTagger.GitHub;
using RepoTagger.GitHub.Dtos;
using RepoTagger.Security.Redaction;
using RepoTagger.Security.Rule;
using RepoTagger.Token;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;


var builder = WebApplication.CreateBuilder(args);




builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<DataContext>();
builder.Services.AddScoped<DatabaseInitializer>();


builder.Services.AddScoped<WebhookJobRepository>();

builder.Services.AddScoped<GitHubIssueProcessor>();

builder.Services.AddHostedService<BackgroundServices>();


builder.Services.AddTransient<GitHubAuthorizationHandler>();

builder.Services.AddSingleton<GitHubAppAuth>();
builder.Services.AddSingleton<TokenService>();


builder.Services.AddSingleton<IRedactionRule, GitHubTokenRule>();

builder.Services.AddSingleton<SecretRedactor>();


builder.Services.AddSingleton<IRedactionRule, AwsAccessKeyRule>();
builder.Services.AddSingleton<IRedactionRule, EmailRule>();
builder.Services.AddSingleton<IRedactionRule, GenericSecretRule>();
builder.Services.AddSingleton<IRedactionRule, PrivateKeyRule>();




builder.Services.AddSingleton<ConfidenceGate>();

builder.Services.AddRepoTaggerKernel(builder.Configuration);
builder.Services.AddSingleton<PromptLoader>();

//uncomment when testing 
//builder.Services.AddScoped<IIssueClassifier, FakeIssueClassifier>();
builder.Services.AddScoped<IIssueClassifier, SemanticKernelIssueClassifier>();


builder.Services.AddScoped<IGitHubIssueClient, GitHubIssueClient>();


builder.Services.AddScoped<IssueDecisionRepository>();




builder.Services.AddScoped<IssueAnalysisPipeline>();
builder.Services.Configure<AIOptions>(builder.Configuration.GetSection("AI"));


builder.Services.AddSingleton<IIssueEmbeddingRepository,IssueEmbeddingRepository>();

//builder.Services.AddScoped<IDuplicateDetector, FakeDuplicateDetector>();
builder.Services.AddScoped<IDuplicateDetector, SemanticKernelDuplicateDetector>();


builder.Services.AddScoped<IssueReconciliationProcessor>();
builder.Services.AddScoped<AiUsageRepository>();
builder.Services.AddScoped<AiPricingRepository>();

builder.Services.AddScoped<RepositoryRateLimiter>();

builder.Services.AddHttpClient("GitHub", client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RepoTagger");
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
}).AddHttpMessageHandler<GitHubAuthorizationHandler>();


builder.Services.AddHttpClient("GitHubApp", client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RepoTagger");
    client.DefaultRequestHeaders.Add(
        "X-GitHub-Api-Version",
        "2022-11-28");
});

var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var initializer =
        scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();

    await initializer.InitializeAsync();
}


app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseHttpsRedirection();

// Only the GitHub webhook is reachable from outside. Dashboard, reports and
// Swagger are local-only — anything else returns 404 rather than revealing it exists.
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/github"))
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip is null || !IPAddress.IsLoopback(ip))
        {
            context.Response.StatusCode = 404;
            return;
        }
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/github/webhook", async (HttpContext context , IConfiguration  configuration,  WebhookJobRepository jobRepository, ILogger<Program> _logger) =>
{

    _logger.LogInformation(   "GitHub webhook received");



    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    var signature =  context.Request.Headers["X-Hub-Signature-256"].ToString();

    var secret = configuration["GitHub:WebHookSecret"];

    if (string.IsNullOrEmpty(secret))
    {
        _logger.LogError("Webhook secret missing");
        return Results.StatusCode(500);
    }

    var isValid = Verify(  signature, body, secret!);

    if (!isValid)
    {
        _logger.LogError("Signature verification failed");
      
        return Results.Unauthorized();
    }

    _logger.LogInformation("Signature Valid");

    var deliveryId = context.Request.Headers["X-GitHub-Delivery"].ToString();

    if (string.IsNullOrWhiteSpace(deliveryId))
    {
        return Results.BadRequest();
    }

    

    var payload = JsonSerializer.Deserialize<GitHubIssueWebhookPayload>(
    body,
    new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    if (payload == null)
    {
        return Results.BadRequest();
    }


    var isNewIssue = payload.Action == "opened";

    var isLabelRemoval =
        payload.Action == "unlabeled" &&
        payload.Label != null &&   payload.Label.Name.StartsWith("ai:", StringComparison.Ordinal);


    if  (!isNewIssue && !isLabelRemoval)
    {
        _logger.LogInformation("Ignoring issue action {Action}", payload.Action);
        return Results.Ok(); // acknowledging  receipt,
    }
   

    var job = new BackgroundJobDto
    {
        DeliveryId = deliveryId,
        EventType = context.Request.Headers["X-GitHub-Event"].ToString(),
        Action = payload.Action,
        Payload = body,
        Status = "Pending",
        Attempts = 0,
        CreatedAtUtc = DateTime.UtcNow,
        StartedAtUtc = null,
        CompletedAtUtc = null,
        LastError = null
    };

    var queued = await jobRepository.TryEnqueueAsync(
     deliveryId,
     job);


    if (!queued)
    {
        return Results.Ok();
    }

    return Results.Ok();

});





app.MapGet("/reports/accuracy", async (IssueDecisionRepository repository,  CancellationToken ct) =>
{

    var report = await repository.GetAccuracyReportAsync(ct);
    return Results.Ok(report);
});

app.MapGet("/reports/operations", async (WebhookJobRepository jobRepository,IssueDecisionRepository decisionRepository, CancellationToken ct) =>
{


    var operations = await jobRepository.GetOperationsReportAsync(ct);
    var breakdown = await decisionRepository.GetIssueBreakdownAsync(ct);
    return Results.Ok(new { operations, breakdown });
});



app.MapGet("/reports/cost/detail", async (AiUsageRepository usageRepository,bool onlyUnknown, CancellationToken ct) =>
{


    var detail = await usageRepository.GetUsageDetailAsync(onlyUnknown, ct);
    return Results.Ok(detail);
});


app.MapGet("/reports/cost", async (AiUsageRepository usageRepository, CancellationToken ct) =>
{
  

    var report = await usageRepository.GetCostReportAsync(ct);
    return Results.Ok(report);
});



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{

    app.UseSwagger();
    app.UseSwaggerUI();

}


app.Run();
    
 static bool Verify(string signatureHeader, string payload, string secret)
{
    if (string.IsNullOrEmpty(signatureHeader))
        return false;

    if (!signatureHeader.StartsWith("sha256="))
        return false;

    var receivedSignature = signatureHeader["sha256=".Length..];

    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));

    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

    var expectedSignature = Convert.ToHexString(hash).ToLowerInvariant();


    return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(receivedSignature));


}

















