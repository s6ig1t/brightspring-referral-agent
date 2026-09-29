using Xunit;

namespace BrightSpring.Referral.Rules.Tests;

public class HighRiskCombinationRuleTests
{
    private readonly HighRiskCombinationRule _rule = new();

    [Fact]
    public void Evaluate_KnownHighRiskPairPresent_ReturnsCriticalFinding()
    {
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("warfarin", 5),
            TestPackets.Med("aspirin", 81),
        });

        var findings = _rule.Evaluate(packet);

        var finding = Assert.Single(findings);
        Assert.Equal(Core.FindingSeverity.Critical, finding.Severity);
        Assert.Contains("bleeding risk", finding.Description);
        Assert.Equal(2, finding.Citations.Count);
    }

    [Fact]
    public void Evaluate_OnlyOneSideOfPairPresent_ReturnsNoFindings()
    {
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("warfarin", 5),
        });

        var findings = _rule.Evaluate(packet);

        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_MultipleHighRiskPairsPresent_ReturnsOneFindingPerPair()
    {
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("warfarin", 5),
            TestPackets.Med("aspirin", 81),
            TestPackets.Med("morphine", 15),
            TestPackets.Med("oxycodone", 10),
        });

        var findings = _rule.Evaluate(packet);

        // warfarin+aspirin and morphine+oxycodone are both on the
        // reference list, so both should surface independently.
        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void Evaluate_NoKnownPairsPresent_ReturnsNoFindings()
    {
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("lisinopril", 10),
            TestPackets.Med("metoprolol", 50),
        });

        var findings = _rule.Evaluate(packet);

        Assert.Empty(findings);
    }
}
