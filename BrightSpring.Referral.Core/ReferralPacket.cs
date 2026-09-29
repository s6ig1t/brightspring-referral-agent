namespace BrightSpring.Referral.Core;

/// <summary>
/// The fully structured, cited representation of an incoming referral —
/// the output of the intake agent and the input to the rules engine.
///
/// This is the seam between the two halves of the pipeline. Everything
/// upstream of a <see cref="ReferralPacket"/> (raw document text, an LLM
/// call, citation bookkeeping) is non-deterministic and lives in
/// BrightSpring.Referral.Agents. Everything downstream of it (dose
/// checks, duplicate-therapy checks, allergy cross-checks) is plain,
/// deterministic C# with no AI dependency and lives in
/// BrightSpring.Referral.Rules. Keeping this record AI-agnostic — no
/// prompt text, no model metadata, nothing but clinical data and
/// citations — is what makes that separation real rather than aspirational.
/// </summary>
public sealed record ReferralPacket(
    string ReferralId,
    string SourceDocumentName,
    PatientDemographics Patient,
    IReadOnlyList<Medication> Medications,
    IReadOnlyList<Allergy> Allergies,
    IReadOnlyList<Diagnosis> Diagnoses,
    DateTimeOffset ReceivedAtUtc);
