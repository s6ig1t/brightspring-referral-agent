using BrightSpring.Referral.Core;

namespace BrightSpring.Referral.Rules;

/// <summary>
/// Flags medication pairs on the reference high-risk interaction list
/// (see <see cref="DrugReferenceData.HighRiskPairs"/>).
///
/// This intentionally does NOT attempt to be a general drug-interaction
/// checker — that is a large, regulated, and continuously-updated body of
/// knowledge that belongs in a licensed interaction database, not a
/// portfolio project's static list. What it demonstrates is the
/// architecture such a checker would plug into: a rule that takes a
/// reference data source as input and reports findings with full
/// citations, with no involvement from the language model.
/// </summary>
public sealed class HighRiskCombinationRule : IReferralRule
{
    public string RuleName => "High-Risk Combination";

    public IReadOnlyList<Finding> Evaluate(ReferralPacket packet)
    {
        var findings = new List<Finding>();
        var medsByName = packet.Medications
            .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var (a, b, riskDescription) in DrugReferenceData.HighRiskPairs)
        {
            if (!medsByName.TryGetValue(a, out var medA) || !medsByName.TryGetValue(b, out var medB))
            {
                continue;
            }

            findings.Add(new Finding(
                Category: FindingCategory.HighRiskCombination,
                Severity: FindingSeverity.Critical,
                Description: $"{medA.Name} and {medB.Name} are both present on this referral: {riskDescription}.",
                Citations: new[] { medA.SourceCitation, medB.SourceCitation }));
        }

        return findings;
    }
}
