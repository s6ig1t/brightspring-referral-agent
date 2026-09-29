using BrightSpring.Referral.Core;

namespace BrightSpring.Referral.Rules;

/// <summary>
/// Aggregates every <see cref="IReferralRule"/> and runs them against a
/// referral packet, returning a single, severity-sorted findings list.
///
/// This class is the one and only entry point the Agents project is
/// allowed to call into this project through. It has no AI dependency,
/// no I/O, and no async work — evaluating a packet is pure, in-memory
/// computation, which is exactly why it can run inside a unit test in
/// milliseconds with no test doubles required.
/// </summary>
public sealed class ReferralRuleEngine
{
    private readonly IReadOnlyList<IReferralRule> _rules;

    /// <summary>
    /// Default constructor wires up the full standard rule set. A
    /// constructor overload that accepts a custom rule list exists mainly
    /// so tests can exercise the aggregation/sorting behavior with a
    /// small number of fake rules in isolation from the real ones.
    /// </summary>
    public ReferralRuleEngine() : this(DefaultRules())
    {
    }

    public ReferralRuleEngine(IReadOnlyList<IReferralRule> rules)
    {
        _rules = rules;
    }

    private static IReadOnlyList<IReferralRule> DefaultRules() => new IReferralRule[]
    {
        new DuplicateTherapyRule(),
        new DoseRangeRule(),
        new AllergyConflictRule(),
        new HighRiskCombinationRule(),
    };

    /// <summary>
    /// Runs every configured rule against the packet and returns the
    /// combined findings, most severe first. Findings of equal severity
    /// preserve the order their rules ran in, so output is stable and
    /// easy to assert on in tests.
    /// </summary>
    public IReadOnlyList<Finding> Evaluate(ReferralPacket packet)
    {
        var all = new List<Finding>();
        foreach (var rule in _rules)
        {
            all.AddRange(rule.Evaluate(packet));
        }

        return all
            .OrderByDescending(f => f.Severity)
            .ToList();
    }
}
