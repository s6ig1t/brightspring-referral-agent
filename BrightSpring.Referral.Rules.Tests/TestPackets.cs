using BrightSpring.Referral.Core;

namespace BrightSpring.Referral.Rules.Tests;

/// <summary>
/// Small factory helpers for building <see cref="ReferralPacket"/>
/// instances in tests without repeating citation/demographics
/// boilerplate in every test method. Every citation produced here points
/// at a fake "test-fixture.txt" document — tests care about the rule
/// logic, not about realistic source text.
/// </summary>
internal static class TestPackets
{
    private static Citation Cite(string snippet, int page = 1) => new("test-fixture.txt", page, snippet);

    public static PatientDemographics Patient(string name = "Test Patient") =>
        new(name, AgeYears: 72, ReferringService: "Home Health", Cite($"Patient: {name}"));

    public static Medication Med(string name, double dose, string unit = "mg", string frequency = "once daily", string route = "oral") =>
        new(name, dose, unit, frequency, route, Cite($"{name} {dose}{unit} {frequency}"));

    public static Allergy Allergy(string substance, string? reaction = null) =>
        new(substance, reaction, Cite($"Allergy: {substance}" + (reaction is null ? "" : $" ({reaction})")));

    /// <summary>
    /// Builds a packet from whichever medications/allergies/diagnoses a
    /// test needs; anything not supplied defaults to an empty list, which
    /// is what most rules should treat as "nothing to flag."
    /// </summary>
    public static ReferralPacket Build(
        IReadOnlyList<Medication>? medications = null,
        IReadOnlyList<Allergy>? allergies = null,
        IReadOnlyList<Diagnosis>? diagnoses = null,
        string referralId = "REF-TEST-0001") =>
        new(
            ReferralId: referralId,
            SourceDocumentName: "test-fixture.txt",
            Patient: Patient(),
            Medications: medications ?? Array.Empty<Medication>(),
            Allergies: allergies ?? Array.Empty<Allergy>(),
            Diagnoses: diagnoses ?? Array.Empty<Diagnosis>(),
            ReceivedAtUtc: DateTimeOffset.UtcNow);
}
