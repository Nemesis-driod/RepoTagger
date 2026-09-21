# RepoTagger

A GitHub bot that reads new issues and applies a label: `bug`, `feature-request`, `question`, or `needs-human-review`. It also flags possible duplicates and tracks what it spent doing it.

It runs as a GitHub App against your own repo, using your own AI provider key. Nothing is sent anywhere except the AI provider you configure.

## What it actually does

When an issue is opened:

1. Title and body are scanned for secrets (API keys, tokens, AWS keys, private keys) and redacted before anything leaves the process.
2. The redacted text is embedded and compared against previous issues in the same repo. Close matches get posted as a comment listing candidate issue numbers.
3. The redacted text is classified. If confidence is too low, the label becomes `needs-human-review` and the bot explains why in a comment.
4. The label is applied, prefixed with `ai:` — `ai:bug`, not `bug`. Your real label taxonomy stays yours.
5. The decision is written to a local SQLite database: label, confidence, the model's stated reason, which model, how many tokens.

The bot only ever adds an allowlisted label or posts a comment. It does not close issues, does not apply arbitrary labels, and does not act on anything it isn't confident about.

## What it won't do

- It won't close an issue as a duplicate. It tells a maintainer what looks similar and stops there.
- It won't apply a label outside the allowlist. If the model invents one, that's treated the same as low confidence.
- It won't follow instructions embedded in issue text. Issue content is data, not instructions, and the prompt says so explicitly. This has been tested against real injection attempts.

## Measuring whether it's actually right

Every decision is recorded. When a human removes an `ai:` label, that's recorded too. The accuracy report gives you the override rate per label — the percentage of the bot's decisions a human later undid.

This is the point of the whole thing. A classifier that says "95% confident" is making a claim about itself. The override rate is the number that actually tells you whether to trust it.

## Setup

### 1. Register a GitHub App

This is the part you can't work out from the code. Go to **Settings → Developer settings → GitHub Apps → New GitHub App**.

- **Webhook URL**: where your instance will receive events. For local development use [smee.io](https://smee.io) — create a channel and use that URL.
- **Webhook secret**: generate a long random string. You'll need it again in config.
- **Permissions → Repository permissions → Issues**: Read & write. That's the only permission needed.
- **Subscribe to events**: Issues.

After creating the app:

- Note the **App ID** on the app's settings page.
- **Generate a private key**. This downloads a `.pem` file. Keep it out of git — `.gitignore` already excludes `*.pem`.
- **Install the app** on the repo you want it to watch. After installing, the URL contains the installation ID: `.../settings/installations/<InstallationId>`.

### 2. Get an AI provider key

Gemini is the tested provider. Get a key from [Google AI Studio](https://aistudio.google.com/apikey).

The provider architecture supports Ollama and OpenAI-compatible endpoints, but only Gemini has been run against real traffic. Treat the others as untested.

### 3. Configure

Copy `appsettings.example.json` to `appsettings.json` and fill it in. `appsettings.json` is gitignored — your real keys stay local.

```json
{
  "ConnectionStrings": {
    "RepoTagger": "Data Source=RepoTagger.db;Foreign Keys=True;Default Timeout=5;"
  },
  "GitHub": {
    "AppId": 123456,
    "ClientId": "your-client-id",
    "WebhookSecret": "the-secret-you-generated",
    "PrivateKey": "path-or-contents-of-your-pem",
    "InstallationId": "from-the-install-url"
  },
  "AI": {
    "Provider": "Gemini",
    "ApiKey": "your-gemini-key",
    "ModelId": "gemini-3.6-flash",
    "EmbeddingModelId": "gemini-embedding-001",
    "EscalationModelId": "gemini-3.1-pro-preview",
    "MaxAiCallsPerDayPerRepository": 100,
    "ConfidenceThreshold": 0.75,
    "DuplicateSimilarityThreshold": 0.85
  }
}
```

`EscalationModelId` is optional — leave it empty to disable cascade routing.

`ConfidenceThreshold` is the line between "apply the label" and "hand it to a human". 0.75 is a sensible start. Push it higher and more issues get routed to a human; push it lower and the bot acts on weaker guesses.

### 4. Run

```
dotnet run
```

Tables are created on first start. Open an issue on the installed repo and watch the logs.

### 5. Create the labels

Before the bot runs, create these four labels in your repo (**Settings → Labels → New label**):

| Label | Suggested color |
|---|---|
| `ai:bug` | `#d73a4a` |
| `ai:feature-request` | `#a2eeef` |
| `ai:question` | `#d876e3` |
| `ai:needs-human-review` | `#fbca04` |

If you skip this, the bot may still work — GitHub's API likely creates a missing label automatically the first time it's applied — but it'll get a random color with no description, and you can't fully rule out a failure on a repo you haven't tested against yet. Five minutes now avoids wondering later why a label never showed up.

## Dashboard

Open the app's root URL in a browser — `https://localhost:5001`, or whatever port Kestrel reports on startup.

It shows job counts, the accuracy report, cost per model, decision breakdown, and individual AI calls. It's read-only. Nothing on it changes configuration.

**Only the `/github/*` webhook path is reachable from outside the machine.** The dashboard, the report endpoints and Swagger all return 404 to anything that isn't a loopback request. There's no login because there's nothing to log into — if you can reach it, you're already on the box.

If you deploy behind a reverse proxy, check that the proxy sets `X-Forwarded-For`. Without it every request looks like it came from the proxy, and if the proxy sits on the same machine that reads as local.

## The database

It's a SQLite file next to the binary, `RepoTagger.db`. It's yours. Nothing backs it up, nothing replicates it, nothing will stop you deleting it. Delete it and you lose every decision, every override, every embedding and every cost record — the bot starts again from empty and no longer knows about previous issues for duplicate detection. That's on you.

Poke at it with the `sqlite3` CLI:

```bash
sqlite3 RepoTagger.db
```

```sql
.tables
.headers on
.mode box

-- what the bot decided recently
SELECT IssueNumber, Label, Confidence, ModelReason FROM IssueDecisions ORDER BY Id DESC LIMIT 10;

-- what it spent
SELECT CallType, Model, TotalTokens, OccurredAtUtc FROM AiUsageEvents ORDER BY Id DESC LIMIT 20;

-- jobs that didn't make it
SELECT Id, Status, Attempts, LastError FROM WebhookJobs WHERE Status = 'Failed';

-- labels a human took back off
SELECT * FROM LabelOverrideEvents;
```

### Prices

Cost is calculated from a local table you fill in yourself. Until you do, the dashboard shows token counts and no dollar figures.

Check your provider's pricing page, then:

```sql
INSERT INTO AiPricing (Provider, Model, InputPricePerMillionTokens, OutputPricePerMillionTokens, EffectiveFromUtc, RecordedAtUtc)
VALUES ('Gemini', 'gemini-3.6-flash', 1.50, 7.50, '2026-01-01 00:00:00', datetime('now'));
```

Never update a price row. Insert a new one with a later `EffectiveFromUtc`. Cost is always calculated with whichever price was active when the call happened, so a price change doesn't rewrite history.

Reasoning tokens are billed at the output rate and counted as output when calculating cost.

**These numbers are an estimate for your own visibility. For what you actually owe, check your provider's console.**

## Other tunables

Most configuration lives in `appsettings.json`. A few values are constants in code instead, because they're implementation detail rather than something you're expected to tune per deployment. If you do want to change one, here's where they live:

| Value | Where | Default | What it does |
|---|---|---|---|
| `MaxEmbeddingInputCharacters` | `AI/SemanticKernelDuplicateDetector.cs` | 8000 | Caps how much title+body text gets sent for embedding. A rough proxy for the provider's token limit, not an exact one. |
| `CandidatesToFetch` / `CandidatesToReturn` | `AI/SemanticKernelDuplicateDetector.cs` | 5 / 3 | How many nearest embeddings get pulled from the vector store, and how many survive filtering to actually appear in a duplicate comment. |
| Retry backoff (30s / 5min / 30min) | `Data/WebhookJobRepository.cs`, `MarkPending` | — | Delay before a failed job's next attempt, by attempt number. |
| Stuck-job threshold | `Data/WebhookJobRepository.cs`, in **both** `ClaimNextJob` and `GetOperationsReportAsync` | 10 minutes | How long a job can sit `Processing` before it's treated as abandoned (crashed worker) and reclaimed. **It's a literal in two places — change both, or the operations report and the actual reclaim behavior will disagree.** |
| Low-confidence report threshold | `Data/IssueDecisionRepository.cs`, `GetIssueBreakdownAsync` | 0.90 | The cutoff the dashboard's "low confidence" count uses — matches the prompt's own boundary between "clear" and "reasonable but uncertain," not an arbitrary pick. |
| Allowed labels | `AI/Domain/AllowedActions.cs` | `bug`, `feature-request`, `question`, `needs-human-review` | Anything the model returns outside this list is treated as low confidence, not applied. |
| `ai:` label prefix | `GitHub/GitHubLabelMapper.cs` | `ai:` | What the bot's labels are prefixed with to stay separate from your real taxonomy. |
| Confidence guidance | `AI/Prompts/IssueClassifier.yaml` | — | The bands the model is told to use when deciding its own confidence. Changing this changes model behavior, not application logic — test carefully. |
| Rate-limit window | `AI/RepositoryRateLimiter.cs` | 24 hours, rolling | Not a calendar day — a 24-hour window measured from each call's own timestamp. |

## Running against multiple repositories

Each instance handles one GitHub App installation with one AI provider key. To cover multiple repositories, run a separate instance for each — its own config file, its own key, its own SQLite file.

This keeps repos fully isolated: one repo's cost, failures, or rate limiting can't touch another. Instances are cheap — SQLite, no external services, no message broker.

Sharing one AI provider key across many busy repos will slow things down, because provider rate limits apply to the key, not the repo. Separate keys avoid that.

## What's rough

- **Token counts are sometimes unknown.** The Gemini connector doesn't report usage for embedding calls. Those are recorded with unknown cost rather than assumed to be zero, and reports count them separately so an incomplete total doesn't look precise.
- **Usage extraction is Gemini-specific.** Point this at another provider and cost tracking degrades to "unknown" rather than breaking. It won't give you a wrong number, it just won't give you a number.
- **Cascade routing is lightly tested.** Escalating a low-confidence result to a stronger model works, but in real traffic the primary model rarely lands in the band that triggers it. If you exercise this path, what you find is genuinely useful — open an issue.
- **SQLite and GitHub can't be written atomically.** If a GitHub call succeeds and the local write immediately after it fails, a retry can repost a comment. The window is small and the design minimizes it, but it isn't zero and can't be.
- **A crashed worker's job looks like a failing job.** Both show as retried. A busy repo hitting its rate limit will inflate the retry rate in the operations report.

## Stack

ASP.NET Core, Semantic Kernel, SQLite with Dapper, sqlite-vec for vector search. No Entity Framework, no external database, no message queue, no frontend build step. The data needs are small and it's built to stay that way.

## License

PolyForm Noncommercial 1.0.0. Free for noncommercial use. Commercial use needs a separate license — open an issue and we'll talk.
