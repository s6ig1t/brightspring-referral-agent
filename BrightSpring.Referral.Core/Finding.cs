namespace BrightSpring.Referral.Core;

/// <summary>
/// Severity of a <see cref="Finding"/>, used both to sort the review
/// brief (most important first) and, in a real deployment, to decide
/// whether a brief can be auto-routed or must block on pharmacist review.
/// </summary>
public enum FindingSeverity
{
    Info,
    Caution,
    Critical
}

/// <summary>
/// The category of check that produced a <see cref="Finding"/>. Kept as
/// an enum rather than a free-text string so the review agent (which only
/// ever narrates findings, never invents them) has a closed set of
/// concepts to write about.
/// </summary>
public enum FindingCategory
{
    DuplicateTherapy,
    DoseOutOfRange,
    AllergyConflict,
    HighRiskCombination
}

/// <summary>
/// A single, already-computed result from the deterministic rules engine.
///
/// This is the single most important type in the project for the
/// following reason: everything in a <see cref="Finding"/> — the
/// category, the severity, the description, the citations — is decided
/// by plain C# in BrightSpring.Referral.Rules before any language model
/// is ever invoked. The review agent (BrightSpring.Referral.Agents)
/// receives a list of these and is only ever asked to turn them into
/// readable prose. It cannot change a severity, invent a finding, or
/// silently drop one — the record is immutable, and the orchestrator
/// passes the review agent's narrative alongside the original findings so
/// a pharmacist can always compare the two.
/// </summary>
public sealed record Finding(
    FindingCategory Category,
    FindingSeverity Severity,
    string Description,
    IReadOnlyList<Citation> Citations);
