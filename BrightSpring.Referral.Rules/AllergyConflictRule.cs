using BrightSpring.Referral.Core;

namespace BrightSpring.Referral.Rules;

/// <summary>
/// Cross-checks each medication against the patient's documented
/// allergies, including class-level allergies (e.g. an allergy to
/// "penicillin" catching a prescription for "amoxicillin").
///
/// This is treated as Critical severity unconditionally: unlike a dose
/// question or a duplicate class, a documented allergy conflict is not
/// something to raise as a passive caution — it always needs a human
/// decision before the referral proceeds.
/// </summary>
public sealed class AllergyConflictRule : IReferralRule
{
    public string RuleName => "Allergy Conflict";

    public IReadOnlyList<Finding> Evaluate(ReferralPacket packet)
    {
        var findings = new List<Finding>();

        foreach (var allergy in packet.Allergies)
        {
            // Direct name match: the allergy substance IS a medication name.
            var directMatches = packet.Medications
                .Where(m => string.Equals(m.Name, allergy.Substance, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Class match: the allergy substance names a class (e.g.
            // "penicillin"), and a medication is a known member of it.
            var classMatches = new List<Medication>();
            if (DrugReferenceData.AllergyClassMembers.TryGetValue(allergy.Substance, out var members))
            {
                classMatches.AddRange(packet.Medications
                    .Where(m => members.Contains(m.Name, StringComparer.OrdinalIgnoreCase)));
            }

            var conflicting = directMatches.Concat(classMatches).Distinct().ToList();

            foreach (var med in conflicting)
            {
                var citations = new List<Citation> { allergy.SourceCitation, med.SourceCitation };
                var reactionNote = string.IsNullOrWhiteSpace(allergy.ReactionDescription)
                    ? string.Empty
                    : $" Documented reaction: {allergy.ReactionDescription}.";

                findings.Add(new Finding(
                    Category: FindingCategory.AllergyConflict,
                    Severity: FindingSeverity.Critical,
                    Description: $"{med.Name} conflicts with a documented allergy to {allergy.Substance}." +
                                  reactionNote + " Do not proceed without prescriber confirmation.",
                    Citations: citations));
            }
        }

        return findings;
    }
}
