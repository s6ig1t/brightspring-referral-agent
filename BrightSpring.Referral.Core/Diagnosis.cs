namespace BrightSpring.Referral.Core;

/// <summary>
/// A diagnosis or problem-list entry relevant to the referral. Carried
/// through mainly so the review brief's narrative has clinical context to
/// reference, not because the deterministic rules act on it directly in
/// this version of the project.
/// </summary>
public sealed record Diagnosis(
    string Description,
    Citation SourceCitation);
