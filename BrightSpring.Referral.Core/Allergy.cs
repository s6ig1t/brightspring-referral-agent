namespace BrightSpring.Referral.Core;

/// <summary>
/// A single documented allergy or adverse reaction.
/// </summary>
public sealed record Allergy(
    string Substance,
    string? ReactionDescription,
    Citation SourceCitation);
