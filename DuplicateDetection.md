# Duplicate Detection — Implementation Notes and Assumptions

## Overview

RepoTagger uses vector embeddings to identify GitHub issues that may be duplicates.

The duplicate-detection workflow is intentionally scoped per GitHub repository. An issue from one repository must not be considered a duplicate of an issue from another repository.

The current implementation uses:

- Gemini `gemini-embedding-001` for embeddings.
- 3072-dimensional embedding vectors.
- SQLite with `sqlite-vec` through `Microsoft.Extensions.VectorData`.
- Cosine distance for vector search.
- A configurable similarity threshold.
- Up to 5 vector-search candidates.
- Up to 3 candidates returned to the caller.
- Repository filtering before duplicate candidates are returned.
- Current-issue exclusion.
- Upsert semantics using `repository#issueNumber` as the vector-store key.

## 1. Repository Isolation

The vector record stores a normalized repository name:

```csharp
[VectorStoreData]
public string Repository { get; init; } = "";
```

Repositories are normalized using:

```csharp
public static string NormalizeRepository(string repository)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(repository);
    return repository.Trim().ToLowerInvariant();
}
```

The vector-store key is:

```csharp
Id = $"{normalizedRepository}#{issueNumber}";
```

Therefore:

```
opcxder/module-prep#19
```

and:

```
another-owner/another-repo#19
```

are different vector records even though both issues have number 19.

Search also applies a repository filter:

```csharp
Filter = record => record.Repository == normalizedRepository
```

This is an important security and correctness boundary.

**Assumption**

The application assumes that the GitHub repository identifier supplied to the duplicate detector is the canonical `owner/repository` name.

Repository normalization is performed before storage and search, so differences in surrounding whitespace or letter casing do not create separate records.

**Testing**

Repository isolation should be tested directly against `IssueEmbeddingRepository`.

The test does not require a second GitHub webhook.

For example:

```
repository A:
owner/repo-a#1

repository B:
owner/repo-b#2
```

A search scoped to `owner/repo-a` must never return the record from `owner/repo-b`.

## 2. Issue Number Is Part of the Vector Identity

The vector-store key is:

```
{normalizedRepository}#{issueNumber}
```

This means the same issue can be safely upserted:

```
opcxder/module-prep#19
```

without creating another vector record.

This is important because background-job retries can cause `StoreAsync` to execute more than once.

The operation is therefore intentionally an upsert rather than an insert-only operation.

## 3. Current Issue Exclusion

The vector repository returns candidates based on similarity.

The duplicate detector then excludes the issue currently being processed:

```csharp
.Where(c => c.IssueNumber != issueNumber)
```

This prevents an issue from being reported as a duplicate of itself.

This filtering is performed in `SemanticKernelDuplicateDetector`, rather than the lower-level vector repository.

That separation is intentional:

- `IssueEmbeddingRepository` performs vector storage/search.
- `SemanticKernelDuplicateDetector` applies duplicate-detection business rules.

## 4. Similarity and Cosine Distance

The vector field is configured as:

```csharp
[VectorStoreVector(
    Dimensions,
    DistanceFunction = DistanceFunction.CosineDistance)]
```

The vector repository receives a distance score from the vector store.

The implementation converts the distance to a similarity value:

```csharp
var similarity = 1d - distance;
```

The reason is that the application exposes:

> higher similarity = more similar

while cosine distance represents the opposite direction:

> lower distance = more similar

Therefore:

| Distance | Similarity |
|---|---|
| 0.00 | 1.00 |
| 0.15 | 0.85 |
| 0.50 | 0.50 |

The duplicate detector currently uses:

```csharp
DuplicateSimilarityThreshold = 0.85;
```

Therefore candidates below 0.85 are rejected.

**Important assumption**

The conversion:

```csharp
1d - distance
```

depends on the vector store returning the expected cosine-distance scale.

The repository currently documents this assumption explicitly and keeps the conversion in one place.

If the underlying connector changes its scoring semantics, this conversion must be revalidated.

## 5. Embedding Dimensions

The application currently assumes:

```csharp
public const int Dimensions = 3072;
```

This is enforced when creating and validating embeddings.

The repository rejects vectors whose length differs from 3072:

```csharp
if (vector.Length != Dimensions)
{
    throw new ArgumentException(...);
}
```

This protects the vector store from receiving an incorrectly shaped vector.

**Why 3072?**

The application currently accepts the embedding model's 3072-dimensional output rather than requesting a reduced dimensionality.

This was an intentional project decision because the current use case is:

> GitHub issue duplicate detection within a repository.

The application therefore does not attempt to reduce the embedding size.

**Important assumption**

3072 is an application-level invariant.

Changing the embedding model can invalidate this assumption if the replacement model produces a different vector size.

If the model changes, the following must be reviewed together:

- `IssueEmbedding.Dimensions`
- vector-store schema
- embedding generator configuration
- existing stored vectors
- validation logic
- tests

Existing vectors should not be assumed to be compatible with a model producing a different dimensionality.

## 6. Embedding Input Length

The duplicate detector combines title and body:

```csharp
return $"{title}\n\n{body}";
```

The embedding API has a model-specific input-token limit.

The application therefore uses an input-character cap before generating the embedding.

The current implementation should treat this character limit as a defensive application boundary, not as an exact token calculation.

**Important distinction**

Characters are not tokens.

For example:

```
8000 characters
```

does not universally mean:

```
2000 tokens
```

Tokenization depends on the actual text and language.

Therefore the character limit should be regarded as a conservative approximation rather than a mathematically exact token limit.

**Unicode consideration**

C# string indexing and slicing operate on UTF-16 code units.

Therefore code such as:

```csharp
combined[..MaxEmbeddingInputCharacters]
```

can theoretically cut through a Unicode surrogate pair.

For example, some Unicode characters are represented by two UTF-16 code units.

If the cut occurs between those two code units, the resulting string can contain an unmatched surrogate.

This is not normally a problem for ordinary English GitHub issues, but it is a real implementation assumption.

If the application needs robust arbitrary-Unicode handling, the truncation logic should be changed to truncate without splitting a Unicode scalar value.

This is currently a defensive edge case rather than a demonstrated production failure.

## 7. Candidate Retrieval Is Deliberately Larger Than Final Output

The duplicate detector currently uses:

```csharp
private const int CandidatesToFetch = 5;
private const int CandidatesToReturn = 3;
```

The vector store therefore retrieves up to five candidates.

The detector then applies:

- self-exclusion,
- similarity threshold,
- maximum return count.

Conceptually:

```
Vector search
     ↓
   top 5
     ↓
remove current issue
     ↓
remove candidates below threshold
     ↓
   take top 3
```

Fetching more candidates than are ultimately returned provides some room for filtering.

## 8. Duplicate Comment Is Best Effort

The duplicate comment is supplementary information.

If duplicate candidates are found, the processor attempts to add a GitHub comment.

Failure of that comment does not currently prevent the core classification/label workflow from continuing.

This is intentional.

A duplicate comment is useful, but the duplicate detector's stored result and classification/label are considered more important than the optional notification.

## 9. `needs-human-review` Comment Is Load-Bearing

The `needs-human-review` comment is treated differently.

When classification produces:

```
needs-human-review
```

the processor adds the explanation comment.

Failure is logged and rethrown.

This means the job can retry rather than recording the issue as completely processed when the human-facing review explanation was never posted.

This distinction is deliberate:

- duplicate comment → best effort
- `needs-human-review` comment → required for successful processing

## 10. Retry and Idempotency

Background jobs may be retried.

The processor therefore checks whether a decision already exists:

```csharp
ExistsAsync(repository, issueNumber)
```

If it does, processing is skipped.

The decision table uses:

```sql
UNIQUE (Repository, IssueNumber)
```

and the repository uses an upsert:

```sql
ON CONFLICT (Repository, IssueNumber) DO UPDATE
```

This prevents multiple decision rows for the same repository/issue pair.

The embedding store also uses an upsert keyed by:

```
repository#issueNumber
```

Therefore the following operations are retry-safe at the local-data level:

- embedding store
- decision record
- label application

GitHub comments are not inherently idempotent through a normal comment POST.

This creates a residual dual-write window if GitHub operations succeed but the final local decision write fails.

The current implementation reduces this risk through the final decision record acting as the completed-processing marker, but it does not provide distributed atomicity between SQLite and GitHub.

## 11. SQLite Database Initialization

The vector collection is explicitly initialized during database startup.

The application calls:

```csharp
EnsureCollectionExistsAsync(...)
```

before normal duplicate-detection operations begin.

The repository itself also protects its operations by ensuring the collection exists before vector access.

This is intentionally defensive.

## 12. What Has Been Verified Manually

The live GitHub testing has already demonstrated:

**Embedding generation**

Gemini embedding requests returned:

```
HTTP 200
```

for:

```
gemini-embedding-001:batchEmbedContents
```

**Classification**

The classifier successfully returned:

```
bug
```

with high confidence.

It also successfully returned:

```
needs-human-review
```

with:

```
confidence = 0.95
```

**GitHub labels**

The application successfully applied:

- `ai:bug`
- `ai:needs-human-review`

**GitHub comments**

The GitHub comment endpoint returned:

```
HTTP 201
```

The `needs-human-review` comment was observed on the GitHub issue.

**Duplicate similarity**

Observed examples included:

```
0.9248
```

which exceeded the configured 0.85 threshold.

Other observed candidates included:

```
0.8421
0.8258
0.6313
0.5767
```

which are below the threshold and therefore should not be reported as duplicates.

**Webhook feedback**

When GitHub generated webhook events as a consequence of adding labels, the webhook handler received the event but correctly ignored non-`opened` actions.

## 13. Known Limitations

The following are known assumptions or limitations rather than current defects:

- The 3072-dimensional vector size is tied to the selected embedding model.
- Changing embedding models requires reviewing existing stored vectors.
- Character limits are only an approximation of token limits.
- UTF-16 substring truncation can theoretically split a surrogate pair.
- SQLite and GitHub writes cannot participate in one atomic transaction.
- Normal GitHub comment creation is not inherently idempotent.
- Similarity threshold 0.85 is an application policy and should be tuned against real repository data.
- Embedding similarity indicates semantic similarity; it does not prove that two issues are duplicates.
- Duplicate candidates are suggestions for maintainers, not automatic duplicate closures.
- The current implementation is scoped to the repository represented by the GitHub owner/repository identifier.

## 14. Recommended Future Improvements

These should not be treated as required before the current workflow is considered complete.

Potential future work includes:

- Measuring precision/recall of the duplicate threshold against real labelled issues.
- Evaluating thresholds other than 0.85.
- Adding explicit model/version metadata to stored embeddings.
- Re-embedding existing records when the embedding model changes.
- Implementing Unicode-safe truncation.
- Tracking individual GitHub operation completion for stronger retry semantics.
- Adding automated integration tests around the complete job processor.
- Recording embedding generation/cost information if required by the project.