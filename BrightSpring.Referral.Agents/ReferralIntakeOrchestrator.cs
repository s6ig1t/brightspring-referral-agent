using BrightSpring.Referral.Core;
using BrightSpring.Referral.Rules;

namespace BrightSpring.Referral.Agents;

/// <summary>
/// Coordinates the full pipeline for a single referral: extract, evaluate,
/// draft, and park for human review. This is the class the Api project
/// actually calls — everything above it (IntakeAgent, ReferralRuleEngine,
/// ReviewAgent, IReviewBriefStore) is a collaborator this class wires
/// together in a fixed order.
///
/// The order itself is the whole point of the architecture:
///
///   1. IntakeAgent.ExtractAsync   — the ONLY step that reads the raw
///                                   document and the ONLY step that
///                                   calls the model with document text.
///   2. ReferralRuleEngine.Evaluate — the ONLY step that decides whether
///                                    anything is wrong. Pure C#, no AI.
///   3. ReviewAgent.DraftNarrativeAsync — writes prose ABOUT the findings
///                                        from step 2. Never sees the raw
///                                        document from step 1.
///   4. IReviewBriefStore.SaveAsync — persists the brief as
///                                    PendingReview. This method returns
///                                    the brief in that state; nothing
///                                    later in this class ever finalizes
///                                    it.
///
/// A production version of this orchestration would likely be expressed
/// as a Microsoft.Agents.AI workflow graph (sequential edges between the
/// intake and review agents, with a request/response node for the human
/// gate) rather than a hand-written method — the sequence would be the
/// same, but the framework would give you built-in checkpointing so a
/// pending review survives a process restart. This class demonstrates the
/// same sequence and the same guarantees explicitly, which is easier to
/// read for a first pass through the codebase.
/// </summary>
public sealed class ReferralIntakeOrchestrator
{
    private readonly IntakeAgent _intakeAgent;
    private readonly ReviewAgent _reviewAgent;
    private readonly ReferralRuleEngine _ruleEngine;
    private readonly IReviewBriefStore _briefStore;
    private readonly IAuditLog _auditLog;

    public ReferralIntakeOrchestrator(
        IntakeAgent intakeAgent,
        ReviewAgent reviewAgent,
        ReferralRuleEngine ruleEngine,
        IReviewBriefStore briefStore,
        IAuditLog auditLog)
    {
        _intakeAgent = intakeAgent;
        _reviewAgent = reviewAgent;
        _ruleEngine = ruleEngine;
        _briefStore = briefStore;
        _auditLog = auditLog;
    }

    public async Task<ReviewBrief> ProcessReferralAsync(
        string sourceDocumentName,
        string documentText,
        CancellationToken cancellationToken = default)
    {
        var referralId = $"REF-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";

        await _auditLog.RecordAsync(referralId, "pipeline.started", "orchestrator", content: sourceDocumentName, cancellationToken: cancellationToken);

        // Step 1: extraction. This is the only step with access to the
        // raw document text.
        var packet = await _intakeAgent.ExtractAsync(referralId, sourceDocumentName, documentText, cancellationToken);

        // Step 2: deterministic evaluation. No AI call happens in this
        // line — it is plain C# over the structured packet from step 1.
        var findings = _ruleEngine.Evaluate(packet);

        // Step 3: narrative drafting from the findings only. Note that
        // `documentText` is never passed to this call.
        var narrative = await _reviewAgent.DraftNarrativeAsync(referralId, packet.Patient, findings, cancellationToken);

        var brief = new ReviewBrief(
            BriefId: $"BRIEF-{Guid.NewGuid():N}",
            ReferralId: referralId,
            PatientSummary: $"{packet.Patient.DisplayName}, age {packet.Patient.AgeYears} — {packet.Patient.ReferringService}",
            Findings: findings,
            NarrativeSummary: narrative,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Status: ApprovalStatus.PendingReview);

        // Step 4: persist as pending. The brief this method returns is
        // never approved or rejected here — see IReviewBriefStore for the
        // only two methods that can change that.
        var saved = await _briefStore.SaveAsync(brief, cancellationToken);

        await _auditLog.RecordAsync(referralId, "pipeline.completed", "orchestrator",
            content: saved.BriefId,
            notes: $"{findings.Count} finding(s), status={saved.Status}",
            cancellationToken: cancellationToken);

        return saved;
    }
}
