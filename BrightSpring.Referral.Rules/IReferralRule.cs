using BrightSpring.Referral.Core;

namespace BrightSpring.Referral.Rules;

/// <summary>
/// Contract for a single deterministic check against a
/// <see cref="ReferralPacket"/>.
///
/// Every implementation of this interface must be a pure function of its
/// input: same packet in, same findings out, every time, with no I/O, no
/// randomness, and no AI call. That constraint is what makes each rule
/// trivially unit-testable and is the whole reason this project has a
/// Rules project that is separate from Agents in the first place.
/// </summary>
public interface IReferralRule
{
    /// <summary>
    /// A short, stable name for the rule, used in logging and test
    /// output.
    /// </summary>
    string RuleName { get; }

    /// <summary>
    /// Evaluates the packet and returns zero or more findings. Returning
    /// an empty list is the expected, common case — most referrals
    /// should not trigger most rules.
    /// </summary>
    IReadOnlyList<Finding> Evaluate(ReferralPacket packet);
}
