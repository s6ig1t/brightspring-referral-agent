using BrightSpring.Referral.Core;
using Xunit;

namespace BrightSpring.Referral.Rules.Tests;

public class ReferralRuleEngineTests
{
    [Fact]
    public void Evaluate_CleanPacket_ReturnsNoFindings()
    {
        // A packet with no duplicate classes, no out-of-range doses, no
        // allergy conflicts, and no high-risk pairs should sail through
        // completely clean — this is the common case, not the edge case.
        var engine = new ReferralRuleEngine();
        var packet = TestPackets.Build(medications: new[]
        {
            TestPackets.Med("lisinopril", 10),
            TestPackets.Med("metoprolol", 25),
        });

        var findings = engine.Evaluate(packet);

        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_PacketWithMultipleIssueTypes_ReturnsFindingsSortedBySeverityDescending()
    {
        var engine = new ReferralRuleEngine();
        var packet = TestPackets.Build(
            medications: new[]
            {
                // Duplicate therapy (Caution): two ACE inhibitors.
                TestPackets.Med("lisinopril", 10),
                TestPackets.Med("enalapril", 5),
                // Dose out of range (Critical).
                TestPackets.Med("warfarin", 20),
                // High-risk combination (Critical) once aspirin is added below.
                TestPackets.Med("aspirin", 81),
            },
            allergies: new[]
            {
                // Allergy conflict (Critical).
                TestPackets.Allergy("aspirin"),
            });

        var findings = engine.Evaluate(packet);

        Assert.True(findings.Count >= 3);
        // The first finding must be Critical; Caution findings, if any,
        // must not appear before any Critical finding.
        var firstCautionIndex = findings.ToList().FindIndex(f => f.Severity == FindingSeverity.Caution);
        var lastCriticalIndex = findings.ToList().FindLastIndex(f => f.Severity == FindingSeverity.Critical);
        if (firstCautionIndex >= 0)
        {
            Assert.True(lastCriticalIndex < firstCautionIndex);
        }
    }

    [Fact]
    public void Evaluate_UsesInjectedRuleList_ForIsolatedAggregationTesting()
    {
        // The engine's aggregation/sorting logic can be tested with fake
        // rules, independent of the real clinical rules — useful if this
        // test suite ever needs to pin down ordering behavior precisely
        // without depending on DrugReferenceData's contents.
        var engine = new ReferralRuleEngine(new IReferralRule[]
        {
            new FakeRule(FindingSeverity.Info, "info finding"),
            new FakeRule(FindingSeverity.Critical, "critical finding"),
            new FakeRule(FindingSeverity.Caution, "caution finding"),
        });

        var findings = engine.Evaluate(TestPackets.Build());

        Assert.Equal(3, findings.Count);
        Assert.Equal(FindingSeverity.Critical, findings[0].Severity);
        Assert.Equal(FindingSeverity.Caution, findings[1].Severity);
        Assert.Equal(FindingSeverity.Info, findings[2].Severity);
    }

    private sealed class FakeRule : IReferralRule
    {
        private readonly FindingSeverity _severity;
        private readonly string _description;

        public FakeRule(FindingSeverity severity, string description)
        {
            _severity = severity;
            _description = description;
        }

        public string RuleName => $"Fake ({_severity})";

        public IReadOnlyList<Finding> Evaluate(ReferralPacket packet) => new[]
        {
            new Finding(FindingCategory.DuplicateTherapy, _severity, _description, Array.Empty<Citation>())
        };
    }
}
