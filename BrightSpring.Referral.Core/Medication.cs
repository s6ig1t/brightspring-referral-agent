namespace BrightSpring.Referral.Core;

/// <summary>
/// A single medication line item extracted from a referral packet.
///
/// <see cref="DoseAmount"/> and <see cref="DoseUnit"/> are split out
/// (rather than left as a free-text dose string) specifically so the
/// deterministic rules engine in BrightSpring.Referral.Rules can compare
/// them against a numeric reference range without re-parsing text or
/// asking a model to do arithmetic. Structuring the data this way at the
/// extraction boundary is what keeps the rules engine's job trivial and
/// testable.
/// </summary>
public sealed record Medication(
    string Name,
    double DoseAmount,
    string DoseUnit,
    string Frequency,
    string Route,
    Citation SourceCitation);
