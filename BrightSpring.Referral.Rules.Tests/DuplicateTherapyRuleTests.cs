using BrightSpring.Referral.Core;
using Xunit;

namespace BrightSpring.Referral.Rules.Tests;

public class DuplicateTherapyRuleTests
{
    private readonly DuplicateTherapyRule _rule = new();

    [Fact]
    public void Evaluate_TwoMedicationsInSameClass_ReturnsOneCautionFinding()
    {
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("lisinopril", 10),
            TestPackets.Med("enalapril", 5),
        });

        var findings = _rule.Evaluate(packet);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingCategory.DuplicateTherapy, finding.Category);
        Assert.Equal(FindingSeverity.Caution, finding.Severity);
        Assert.Contains("ACE inhibitor", finding.Description);
        // Both offending medications' citations should be attached, so a
        // reviewer can see exactly where each one came from.
        Assert.Equal(2, finding.Citations.Count);
    }

    [Fact]
    public void Evaluate_SingleMedicationPerClass_ReturnsNoFindings()
    {
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("lisinopril", 10),
            TestPackets.Med("metoprolol", 50),
        });

        var findings = _rule.Evaluate(packet);

        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_UnrecognizedMedicationName_IsIgnoredRatherThanFlagged()
    {
        // A drug we don't have in the reference table should never be
        // treated as evidence of duplication — that would be a false
        // positive caused by an incomplete lookup table, not a real
        // clinical finding.
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("some-unlisted-drug", 10),
            TestPackets.Med("another-unlisted-drug", 10),
        });

        var findings = _rule.Evaluate(packet);

        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_ThreeMedicationsInSameClass_ListsAllThreeByName()
    {
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("morphine", 30),
            TestPackets.Med("oxycodone", 20),
            TestPackets.Med("hydrocodone", 10),
        });

        var findings = _rule.Evaluate(packet);

        var finding = Assert.Single(findings);
        Assert.Contains("morphine", finding.Description);
        Assert.Contains("oxycodone", finding.Description);
        Assert.Contains("hydrocodone", finding.Description);
        Assert.Equal(3, finding.Citations.Count);
    }

    [Fact]
    public void Evaluate_EmptyMedicationList_ReturnsNoFindings()
    {
        var packet = TestPackets.Build();

        var findings = _rule.Evaluate(packet);

        Assert.Empty(findings);
    }
}
