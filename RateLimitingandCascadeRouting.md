# Rate Limiting and Cascade Routing — Known Limitations

Companion to the reliability and cost-tracking docs. This one is short on purpose — it covers only what the two most recent pieces add to the known-gaps list, not a restatement of anything already written up elsewhere.

## 1. A Deferred Job's Retry Count Looks the Same as a Failing Job's

When a repository is over its daily limit, its job is deferred rather than failed — the same `Status = 'Pending'` a genuinely failing job gets while waiting to retry. `Attempts` increments identically in both cases, because the same claim query reclaims both kinds of job.

This means a busy-but-perfectly-healthy repository that regularly bumps against its limit will show up in any report counting "jobs with `Attempts > 1`" as if it were unreliable, when it isn't. The two situations are genuinely different — one is the system working exactly as designed, the other is something going wrong — and right now there's no column that tells them apart at a glance.

## 2. The Daily Limit Counts AI Calls, Not Issues, and Not Cost

`MaxAiCallsPerDayPerRepository` counts every row in the usage table for a repository — embedding calls and classification calls together, with no distinction between them. A single ordinary issue already uses two of those calls (one embedding, one classification) before cascade routing is even considered. An issue that triggers escalation uses three. So "300 calls a day" is not "300 issues a day" — it's closer to 150 issues if none of them escalate, and fewer than that as more of them do.

This isn't a bug in the limit — it's doing exactly what it was built to do, which is bound total AI call volume. But the relationship between "the number configured" and "the number of issues that can actually be processed" isn't fixed, and shifts every time the escalation rate changes. Worth knowing before assuming the configured number maps directly to a daily issue count.

## 3. Escalation Cost Is Not Guaranteed to Change the Outcome

Escalation is deliberately narrowed to fire only when the confidence gate itself rejected an otherwise-valid label — not when the model directly and confidently chose `needs-human-review` on its own. That narrowing rules out the clearest case where a second call would be wasted money.

It does not guarantee the second call helps. Some issues are genuinely short on information in a way no model, however capable, can resolve by being asked twice. For those, escalation still costs a full second call and may return the same low-confidence result. This is an accepted cost of the design, not something the current trigger logic can distinguish in advance — there's no way to know whether escalation will help a specific issue until after paying for it.