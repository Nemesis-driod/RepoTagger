# RepoTagger Duplicate Detection — End-to-End Flow

## 1. GitHub Event

A GitHub issue is opened.

GitHub sends the webhook through the existing smee.io development channel to:

```
POST /github/webhook
```

The webhook endpoint:

- Reads the request body.
- Reads `X-Hub-Signature-256`.
- Verifies the GitHub webhook signature.
- Reads `X-GitHub-Delivery`.
- Deserializes the payload.
- Ignores actions other than `opened`.
- Creates a background job.
- Enqueues the job.

The HTTP webhook endpoint does not perform AI analysis directly.

## 2. Background Job

The background worker receives the queued job.

`GitHubIssueProcessor.ProcessJob` begins processing.

It validates:

- repository
- issue
- issue number

The repository and issue number form the identity used by the downstream duplicate and decision systems.

## 3. Idempotency Check

Before doing expensive processing, the processor checks:

```
Does IssueDecisions already contain this repository + issue number?
```

If yes:

- skip processing
- return

This prevents a completed job from performing the AI and GitHub operations again during a retry.

## 4. Issue Analysis Pipeline

The processor calls:

```csharp
IssueAnalysisPipeline.AnalyzeAsync(...)
```

The pipeline receives:

- repository
- GitHub issue

The title and body are passed through the secret redactor.

Conceptually:

```
GitHub title
     ↓
SecretRedactor
     ↓
redacted title

GitHub body
     ↓
SecretRedactor
     ↓
redacted body
```

The application logs only the number of redaction findings, not the original sensitive values.

## 5. Duplicate Detection

The pipeline passes the redacted issue to:

```csharp
IDuplicateDetector.FindAsync(...)
```

The concrete implementation is:

```
SemanticKernelDuplicateDetector
```

The detector receives:

- repository
- issueNumber
- redactedTitle
- redactedBody

## 6. Build Embedding Input

The title and body are combined:

```
title

body
```

The combined input is passed to the embedding generator.

The application uses Microsoft's provider-independent embedding abstraction:

```csharp
IEmbeddingGenerator<string, Embedding<float>>
```

The configured Gemini embedding model generates a vector.

## 7. Gemini Embedding Request

The application sends the embedding request to Gemini.

The observed request is:

```
POST
https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:batchEmbedContents
```

Successful tests returned:

```
HTTP 200
```

The resulting vector is expected to contain:

```
3072 floats
```

The application validates the vector length before storing it.

## 8. Vector Search

The generated query vector is sent to:

```csharp
IssueEmbeddingRepository.FindSimilarAsync(...)
```

The repository:

- Validates the repository name.
- Validates `top`.
- Validates vector dimensions.
- Ensures the vector collection exists.
- Normalizes the repository name.
- Applies a repository filter.
- Performs vector similarity search.

The search is therefore scoped to:

```
same repository
```

rather than the entire database.

## 9. Repository Isolation

For example:

```
opcxder/module-prep#19
opcxder/module-prep#21
another/repository#19
```

When processing:

```
opcxder/module-prep#23
```

only embeddings belonging to:

```
opcxder/module-prep
```

are eligible.

The issue number alone is not used as the repository boundary.

This means a future repository can use the same SQLite database without its issues becoming duplicate candidates for another repository.

## 10. Similarity Conversion

The vector store uses cosine distance.

The repository converts the returned distance:

```csharp
similarity = 1d - distance;
```

Therefore:

```
lower distance
        ↓
higher similarity
```

The application exposes the result as:

> higher similarity = more similar

## 11. Candidate Filtering

The detector receives up to five candidates.

It then:

```
remove current issue
        ↓
remove similarity < threshold
        ↓
return maximum 3 candidates
```

The current configured threshold is:

```
0.85
```

So:

| Similarity | Result |
|---|---|
| 0.92 | duplicate candidate |
| 0.88 | duplicate candidate |
| 0.85 | duplicate candidate |
| 0.84 | not returned |
| 0.60 | not returned |

## 12. Classification

After duplicate detection, the same redacted title/body are passed to the issue classifier.

The classifier uses the configured Gemini chat model.

The observed request is:

```
POST
https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent
```

The classifier returns:

- Label
- Confidence
- Reason

Allowed labels currently are:

- `bug`
- `feature-request`
- `question`
- `needs-human-review`

## 13. Duplicate Comment

If duplicate candidates remain after filtering:

```csharp
analysis.Duplicates.Any()
```

the processor creates a GitHub comment containing the candidate issue numbers and similarity values.

Example:

```
This issue looks similar to:
#19 (92%).
A maintainer should confirm before closing as duplicate.
```

This comment is supplementary.

Failure of this comment is currently handled as best effort.

## 14. GitHub Label

The classification result is mapped to a GitHub label.

For example:

```
bug
    ↓
ai:bug
```

or:

```
needs-human-review
    ↓
ai:needs-human-review
```

The GitHub API successfully returned:

```
HTTP 200
```

during live testing.

## 15. Human Review Comment

If the classifier returns:

```
needs-human-review
```

the processor posts the human-review explanation.

This comment is load-bearing.

If posting it fails, the processor logs the failure and throws so that the background-job retry mechanism can retry the job.

A successful live test produced:

```
Label = needs-human-review
Confidence = 0.95
```

followed by:

```
POST /issues/24/comments
HTTP 201
```

and the expected comment appeared on GitHub.

## 16. Store Embedding

After the GitHub operations succeed, the processor stores the issue embedding.

The storage key is:

```
{normalizedRepository}#{issueNumber}
```

For example:

```
opcxder/module-prep#24
```

The vector store uses upsert semantics.

Therefore retrying the storage operation does not intentionally create a second vector record for the same issue.

## 17. Store Decision

The final local operation is the issue decision record.

The decision is stored using:

```
Repository + IssueNumber
```

as the logical unique identity.

The table has:

```sql
UNIQUE (Repository, IssueNumber)
```

and the repository performs an upsert.

This record is also used by the processor as the completed-processing marker.

The intended ordering is:

```
GitHub operations
        ↓
embedding storage
        ↓
decision storage
```

The decision record is deliberately last.

## Current End-to-End Flow

The complete workflow can be represented as:

```
GitHub issue opened
        │
        ▼
GitHub webhook
        │
        ▼
/github/webhook
        │
        ├── verify signature
        ├── validate payload
        └── enqueue background job
                │
                ▼
        GitHubIssueProcessor
                │
                ├── validate job
                │
                ├── ExistsAsync?
                │       │
                │       └── yes → stop
                │
                ▼
        IssueAnalysisPipeline
                │
                ├── redact title/body
                │
                ├── generate embedding
                │
                ├── vector search
                │       │
                │       ├── same repository
                │       ├── cosine similarity
                │       ├── exclude current issue
                │       ├── threshold >= 0.85
                │       └── max 3 results
                │
                └── classify issue
                        │
                        ▼
                GitHubIssueProcessor
                        │
                        ├── duplicate comment
                        │
                        ├── apply label
                        │
                        ├── human-review comment
                        │
                        ├── store embedding
                        │
                        └── store decision
                                │
                                ▼
                              DONE
```

## Tests Already Performed

### Test 1 — Normal bug classification

Observed:

```
Label = bug
Confidence = 0.98
```

GitHub result:

```
ai:bug
```

Embedding generation returned:

```
HTTP 200
```

Classification returned:

```
HTTP 200
```

Label application returned:

```
HTTP 200
```

### Test 2 — Duplicate detection

A live issue produced:

```
similarity = 0.924849...
```

This is above:

```
0.85
```

and therefore qualifies as a duplicate candidate.

Other observed values included:

```
0.8421
0.8258
0.6313
0.5767
```

which are below the threshold.

### Test 3 — needs-human-review

Observed:

```
Label = needs-human-review
Confidence = 0.95
```

GitHub successfully received:

```
ai:needs-human-review
```

and the human-review comment.

The GitHub comment API returned:

```
HTTP 201
```

This test passed.

### Test 4 — Webhook feedback from GitHub

When GitHub generated an event after a label was added, the application received the webhook.

The action was:

```
labeled
```

The application correctly logged:

```
Ignoring issue action labeled
```

and did not process the label event as a new issue.

This prevents the bot from recursively processing its own label changes.

## Remaining Automated Tests

Before considering the duplicate-detection implementation fully tested, the highest-value automated tests are:

- Repository isolation.
- Current-issue exclusion.
- Similarity threshold behavior.
- Decision-record uniqueness/upsert.
- `ExistsAsync` behavior.
- Processor retry/idempotency.

A second GitHub repository is not required for repository-isolation testing.

Repository isolation can be tested directly by inserting embeddings for two repository names into the vector store and verifying that a search for repository A cannot return repository B's records.

## Testing Philosophy

The project should distinguish between:

```
unit/integration tests
```

and:

```
live GitHub tests
```

Live GitHub tests have already established that the complete external workflow works.

Automated tests should now focus on deterministic rules that should not depend on Gemini or GitHub availability:

- repository filtering,
- identity,
- self-exclusion,
- thresholds,
- retry behavior,
- upsert behavior.

This avoids turning every test run into an AI/API integration test and makes failures easier to diagnose.