# Reliability, Retry Behavior, and Known Limitations

This document describes how the GitHub issue automation pipeline handles retries, duplicate webhook deliveries, worker crashes, and stale data — and where it deliberately stops short of solving a problem completely.

Some of these are permanent, structural boundaries: SQLite and GitHub can't be updated in one atomic transaction, and no amount of engineering changes that. Others are things left unbuilt because the cost didn't match the value at this stage, not because they were missed. Both kinds are listed here explicitly, rather than left as things you'd only discover by hitting them.

## 1. Duplicate Webhook Deliveries Are Prevented at the Database Level

GitHub can, and does, redeliver the same webhook event more than once — after a timeout on their end, a retry, or a manually redelivered event from their UI.

`WebhookDeliveries.DeliveryId` is the table's primary key. Enqueuing a job inserts the delivery record and the job record in a single transaction, using `INSERT OR IGNORE` on the delivery row. If that delivery ID already exists, the insert is a no-op, the transaction rolls back, and no duplicate job gets created.

This only protects against redelivery of the *same* event. It has nothing to do with idempotency further down the pipeline — that's handled separately, in sections 4 and 5.

## 2. Crashed or Restarted Workers Don't Lose Jobs

A job is claimed with a single atomic statement:

```sql
UPDATE WebhookJobs SET Status = 'Processing', ...
WHERE Id = (SELECT Id FROM WebhookJobs WHERE Status = 'Pending' ... LIMIT 1)
RETURNING *;
```

If the process crashes, gets redeployed, or is killed by the host after claiming a job but before finishing it, that job would otherwise sit in `Processing` forever — nothing would ever pick it back up.

The same claim query also reclaims jobs that have been stuck in `Processing` for more than 10 minutes, as long as they haven't exhausted their retry budget:

```sql
OR (Status = 'Processing' AND StartedAtUtc <= @StuckThreshold AND Attempts < 3)
```

`Attempts` represents the number of times the worker has claimed the job for processing, including reclaim attempts after a worker crash — not just attempts that ran to completion and failed.

**Known gap:** if a job crashes the worker the same way on every attempt, it eventually reaches its retry limit while still stuck in `Processing`. At that point it simply stops being reclaimed — it does not get marked `Failed`, so it isn't currently visible in any failure count. It's silently stuck rather than retried forever, which is a smaller problem, but not a fully closed one.

## 3. Retries Use Increasing Backoff, Not Immediate Retry

A failed job is retried with delay based on attempt count — 30 seconds, then 5 minutes, then 30 minutes — and only marked permanently `Failed` after 3 attempts.

The three backoff delays alone add up to roughly 35 minutes. A failed job may therefore remain retryable for approximately 35 minutes or longer, depending on processing and scheduling delays on top of that. Several other decisions in this document exist specifically because that gap is real, not theoretical.

## 4. Not Every Action Is Treated as Equally Critical to "Job Completed"

A single issue can trigger several GitHub actions: an optional duplicate-candidate comment, applying a label, and — if the classification is `needs-human-review` — an explanation comment. These don't all fail the same way.

- **Applying the label** and the **needs-human-review comment** are required. If either fails, the error is logged and re-thrown, and the job retries.
- **The duplicate-candidate comment** is best-effort. If it fails, the failure is logged and swallowed — it does not block the job from completing.

The reasoning: a missing "this might be a duplicate" note is a minor loss. A missing explanation of *why* an issue needs human review, with no signal at all, is not — a maintainer would have no way to know it needed attention. The `IssueDecisions` row, which marks a job as fully processed and prevents reprocessing on retry, is only ever written after every action treated as required has actually succeeded.

## 5. Reconciliation Doesn't Depend on Write Order

`ExistsAsync()` prevents an issue from being classified more than once — once an `IssueDecisions` row exists, that issue won't be automatically reclassified. If repeated classification of the same issue becomes a real requirement later, this data model will need to change to support decision history rather than a single current decision.

AI-generated labels use an `ai:` prefix (`ai:bug`, not `bug`), kept deliberately separate from a repository's real label taxonomy. A human adding `bug` doesn't conflict with the bot later adding `ai:bug` — they're independent, and the system makes no attempt to infer whether a human label represents agreement or disagreement with the AI's.

When an `ai:`-prefixed label is removed, that's treated as the closest thing to an explicit correction signal GitHub gives us. It's recorded as a `LabelOverrideEvent`, independently of the original decision:

```
AI decision written → label later removed        (the usual order)
label removed → AI decision written afterward     (also possible, given the retry window in §3)
```

Both orders are handled correctly, because the two facts never need to reference each other at write time — the relationship is resolved when a report runs, by matching `RemovedLabel = 'ai:' || Decision.Label`. Reconciliation accuracy doesn't depend on which write happens first.

**What this signal does and doesn't mean:** an override event only records that a label was removed. It doesn't know whether that meant genuine disagreement, a label cleanup pass, an accidental removal, or something else entirely. Override statistics should be read as a correction signal, not a precise measurement of human agreement — and the absence of a removal isn't proof of agreement either, since a human may simply not have looked yet.

**Known source of noise:** if a label is removed and the same label added back shortly after, the removal is still recorded as an override — nothing currently correlates removals with a subsequent re-addition. Building that correlation would mean reconstructing a full label lifecycle for a fairly rare scenario. Accepted trade-off, not an oversight.

## 6. Jobs Are Checked Against Current GitHub State Before Acting

A webhook payload is a snapshot of an issue's state at the moment GitHub sent it. Given the retry window in §3, a job can be processed long enough afterward for the issue to have been closed in the meantime.

Before doing any AI processing or GitHub side effects, the processor re-fetches the issue's current state and skips the job if it's no longer open.

**This check has its own, smaller race window, which is not closed:** the state can still change in the moment between the check and the action.

```
GET issue → open
      ↓
(issue gets closed here)
      ↓
label gets applied anyway
```

The purpose of this check is not to guarantee that the issue cannot change after the check. Its purpose is to prevent the most common and avoidable form of stale processing: executing a delayed job against an issue that is already known to be closed.

This isn't treated as a distributed transaction, because it isn't one — nothing can make a SQLite write and a GitHub API call succeed or fail together. The state check narrows the window from "stale by up to 35 minutes or longer" down to "stale by a few hundred milliseconds."

## 7. A Human Manually Typing an `ai:`-Prefixed Label Is Indistinguishable From the Bot's Own Decision

If a human manually applies something like `ai:bug` themselves — unlikely, but not prevented — and it's later removed, there's no way to tell that apart from removal of a label the bot actually applied. GitHub's API doesn't track *who* added a given label. Left unhandled deliberately: it requires a human doing something with no normal reason to do it, and the cost of being wrong is one noisy data point in an aggregate report, not an incorrect action anywhere in the system.

## 8. GitHub API Availability

GitHub is an external dependency and may be temporarily unavailable, rate-limited, or return transient errors.

GitHub API failures during required processing are treated as job failures and are retried according to the job retry policy.

The system does not assume that a GitHub API request will succeed simply because the issue was successfully received through the webhook.

The system also does not attempt to queue an unlimited number of retries. After the configured retry budget is exhausted, the job requires operational attention — with the one exception noted in §2, where a job that stays stuck in `Processing` across all its attempts currently exhausts its retries without ever reaching `Failed`, and so isn't yet visible the same way a normally-failed job is.