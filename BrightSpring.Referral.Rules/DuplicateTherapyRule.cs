using BrightSpring.Referral.Core;

namespace BrightSpring.Referral.Rules;

/// <summary>
/// Flags two or more medications on the same referral that belong to the
/// same therapeutic class.
///
/// This is one of the most common real findings in medication
/// reconciliation: a patient's referral lists a med from their cardiologist
/// and a differently-named med from their primary care record, and no one
/// has yet noticed they are both, say, ACE inhibitors. Catching this kind
/// of overlap before a pharmacist has to manually cross-reference the med
/// list is a concrete, explainable win for an intake tool.
/// </summary>
public sealed class DuplicateTherapyRule : IReferralRule
{
    public string RuleName => "Duplicate Therapy";

    public IReadOnlyList<Finding> Evaluate(ReferralPacket packet)
    {
        var findings = new List<Finding>();

        // Group medications by therapeutic class, ignoring any medication
        // we don't recognize — an unrecognized drug isn't evidence of
        // duplication, it's evidence our reference table is incomplete.
        var byClass = packet.Medications
            .Select(m => (Medication: m, Class: DrugReferenceData.TherapeuticClass.GetValueOrDefault(m.Name)))
            .Where(x => x.Class is not null)
            .GroupBy(x => x.Class!, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byClass)
        {
            var meds = group.Select(x => x.Medication).ToList();
            if (meds.Count < 2)
            {
                continue;
            }

            var names = string.Join(", ", meds.Select(m => m.Name));
            findings.Add(new Finding(
                Category: FindingCategory.DuplicateTherapy,
                Severity: FindingSeverity.Caution,
                Description: $"Multiple {group.Key} medications on this referral ({names}). " +
                              "Confirm whether this is an intentional combination therapy or an " +
                              "unreconciled duplicate from a prior prescriber.",
                Citations: meds.Select(m => m.SourceCitation).ToList()));
        }

        return findings;
    }
}
