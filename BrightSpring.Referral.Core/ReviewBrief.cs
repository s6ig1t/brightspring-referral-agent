namespace BrightSpring.Referral.Core;

/// <summary>
/// Where a <see cref="ReviewBrief"/> sits in the human-in-the-loop
/// approval flow. A brief is never auto-finalized: it starts at
/// <see cref="PendingReview"/> and only leaves that state when a named
/// reviewer calls the approve or reject endpoint.
/// </summary>
public enum ApprovalStatus
{
    PendingReview,
    Approved,
    Rejected
}

/// <summary>
/// The finished artifact of the pipeline: a narrative summary a
/// pharmacist or intake nurse actually reads, backed by the
/// deterministic findings it was generated from.
///
/// <see cref="NarrativeSummary"/> is the only AI-generated free text in
/// this record, and it is generated strictly from <see cref="Findings"/>
/// — the review agent is never shown the raw referral document when
/// drafting it, only the already-computed findings and patient summary.
/// Keeping both the narrative and the structured findings on the same
/// record means a reviewer never has to take the narrative on faith: the
/// numbers it's describing are sitting right next to it.
/// </summary>
public sealed record ReviewBrief(
    string BriefId,
    string ReferralId,
    string PatientSummary,
    IReadOnlyList<Finding> Findings,
    string NarrativeSummary,
    DateTimeOffset GeneratedAtUtc,
    ApprovalStatus Status,
    string? ReviewedBy = null,
    DateTimeOffset? ReviewedAtUtc = null,
    string? ReviewerNotes = null)
{
    /// <summary>
    /// A brief is routed to mandatory pharmacist review whenever it
    /// contains at least one Critical finding. This is a convenience
    /// property for the API layer; it does not change approval state by
    /// itself — approval always requires an explicit reviewer action.
    /// </summary>
    public bool HasCriticalFindings => Findings.Any(f => f.Severity == FindingSeverity.Critical);
}
