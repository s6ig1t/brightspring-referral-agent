using BrightSpring.Referral.Core;

namespace BrightSpring.Referral.Rules;

/// <summary>
/// Flags a medication whose extracted dose amount exceeds the reference
/// maximum daily dose for that drug.
///
/// This rule is the clearest illustration of the project's central design
/// rule: the extraction agent only ever reports a number it read off the
/// page (<see cref="Medication.DoseAmount"/>), and this rule is the only
/// thing that decides whether that number is a problem. The comparison is
/// a single line of arithmetic — deliberately, since there is no reason
/// to ever let a language model be in the loop for "is 400 greater than
/// 40."
///
/// Only medications given in milligrams are checked in this version; the
/// reference table in <see cref="DrugReferenceData"/> is mg-only, and a
/// unit mismatch (e.g. an extraction that reports "mcg" for a drug the
/// table lists in "mg") is intentionally left unflagged rather than
/// silently compared across units — see the unit-mismatch test in
/// BrightSpring.Referral.Rules.Tests for the reasoning.
/// </summary>
public sealed class DoseRangeRule : IReferralRule
{
    private const string SupportedUnit = "mg";

    public string RuleName => "Dose Range";

    public IReadOnlyList<Finding> Evaluate(ReferralPacket packet)
    {
        var findings = new List<Finding>();

        foreach (var med in packet.Medications)
        {
            if (!string.Equals(med.DoseUnit, SupportedUnit, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!DrugReferenceData.MaxDailyDoseMg.TryGetValue(med.Name, out var maxDailyDose))
            {
                continue;
            }

            if (med.DoseAmount <= maxDailyDose)
            {
                continue;
            }

            findings.Add(new Finding(
                Category: FindingCategory.DoseOutOfRange,
                Severity: FindingSeverity.Critical,
                Description: $"{med.Name} is documented at {med.DoseAmount}{med.DoseUnit}, which exceeds the " +
                              $"reference maximum daily dose of {maxDailyDose}{SupportedUnit}. Verify the " +
                              "prescribed dose and frequency against the source document before proceeding.",
                Citations: new[] { med.SourceCitation }));
        }

        return findings;
    }
}
