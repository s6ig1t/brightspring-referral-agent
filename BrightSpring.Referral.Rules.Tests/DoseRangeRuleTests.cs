using BrightSpring.Referral.Core;
using Xunit;

namespace BrightSpring.Referral.Rules.Tests;

public class DoseRangeRuleTests
{
    private readonly DoseRangeRule _rule = new();

    [Fact]
    public void Evaluate_DoseWithinReferenceRange_ReturnsNoFindings()
    {
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("lisinopril", 20, "mg"), // reference max is 40mg
        });

        var findings = _rule.Evaluate(packet);

        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_DoseExceedsReferenceMax_ReturnsCriticalFinding()
    {
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("lisinopril", 80, "mg"), // reference max is 40mg
        });

        var findings = _rule.Evaluate(packet);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingCategory.DoseOutOfRange, finding.Category);
        Assert.Equal(FindingSeverity.Critical, finding.Severity);
        Assert.Contains("80mg", finding.Description);
        Assert.Contains("40mg", finding.Description);
    }

    [Fact]
    public void Evaluate_DoseExactlyAtMax_ReturnsNoFindings()
    {
        // The rule uses a strict "greater than," so a dose that exactly
        // equals the reference ceiling should not be flagged.
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("lisinopril", 40, "mg"),
        });

        var findings = _rule.Evaluate(packet);

        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_UnitOtherThanMg_IsSkippedRatherThanMiscompared()
    {
        // The reference table is mg-only. A dose reported in a different
        // unit must never be silently compared against an mg ceiling —
        // that would produce a meaningless result that looks authoritative.
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("lisinopril", 5000, "mcg"),
        });

        var findings = _rule.Evaluate(packet);

        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_MedicationNotInReferenceTable_IsSkipped()
    {
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("some-unlisted-drug", 99999, "mg"),
        });

        var findings = _rule.Evaluate(packet);

        Assert.Empty(findings);
    }
}
