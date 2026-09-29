namespace BrightSpring.Referral.Core;

/// <summary>
/// Minimal patient identity needed to route and file a referral.
///
/// Deliberately thin. This project extracts only what the rules engine
/// and the review brief actually need — it is not attempting to model a
/// full clinical record. Anything here is exactly the kind of field that,
/// in a real system, would be subject to PHI-handling rules; see
/// <see cref="BrightSpring.Referral.Agents.PhiRedactor"/> for how this
/// project keeps identifiers out of logs and telemetry.
/// </summary>
public sealed record PatientDemographics(
    string DisplayName,
    int AgeYears,
    string ReferringService,
    Citation SourceCitation);
