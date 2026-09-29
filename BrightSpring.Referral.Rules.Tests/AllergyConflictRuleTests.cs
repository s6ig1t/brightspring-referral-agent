using BrightSpring.Referral.Core;
using Xunit;

namespace BrightSpring.Referral.Rules.Tests;

public class AllergyConflictRuleTests
{
    private readonly AllergyConflictRule _rule = new();

    [Fact]
    public void Evaluate_DirectNameMatch_ReturnsCriticalFinding()
    {
        var packet = TestPackets.Build(
            medications: new[] { TestPackets.Med("sertraline", 50) },
            allergies: new[] { TestPackets.Allergy("sertraline", "hives") });

        var findings = _rule.Evaluate(packet);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingCategory.AllergyConflict, finding.Category);
        Assert.Equal(FindingSeverity.Critical, finding.Severity);
        Assert.Contains("hives", finding.Description);
        // Both the allergy record and the conflicting medication should
        // be cited, so a reviewer can jump to either source line.
        Assert.Equal(2, finding.Citations.Count);
    }

    [Fact]
    public void Evaluate_ClassLevelAllergyMatch_CatchesDifferentDrugInSameClass()
    {
        // Patient is allergic to "penicillin" generally; the referral
        // prescribes amoxicillin, a penicillin-class drug, not literally
        // named "penicillin." This is the case a naive exact-string-match
        // check would miss.
        var packet = TestPackets.Build(
            medications: new[] { TestPackets.Med("amoxicillin", 500) },
            allergies: new[] { TestPackets.Allergy("penicillin", "anaphylaxis") });

        var findings = _rule.Evaluate(packet);

        var finding = Assert.Single(findings);
        Assert.Contains("amoxicillin", finding.Description);
        Assert.Contains("penicillin", finding.Description);
    }

    [Fact]
    public void Evaluate_NoOverlapBetweenMedicationsAndAllergies_ReturnsNoFindings()
    {
        var packet = TestPackets.Build(
            medications: new[] { TestPackets.Med("lisinopril", 10) },
            allergies: new[] { TestPackets.Allergy("penicillin") });

        var findings = _rule.Evaluate(packet);

        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_NoAllergiesDocumented_ReturnsNoFindings()
    {
        var packet = TestPackets.Build(medications: new[] { TestPackets.Med("amoxicillin", 500) });

        var findings = _rule.Evaluate(packet);

        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_AllergyWithNoReactionDescription_StillFlagsWithoutThrowing()
    {
        var packet = TestPackets.Build(
            medications: new[] { TestPackets.Med("sertraline", 50) },
            allergies: new[] { TestPackets.Allergy("sertraline", reaction: null) });

        var findings = _rule.Evaluate(packet);

        Assert.Single(findings);
    }
}
