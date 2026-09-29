using System.Text;
using BrightSpring.Referral.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace BrightSpring.Referral.Agents;

/// <summary>
/// Drafts the narrative summary of a <see cref="ReviewBrief"/> from
/// already-computed <see cref="Finding"/> objects.
///
/// This class is the other half of the project's central design rule —
/// paired with <see cref="IntakeAgent"/>, which reads the document, this
/// agent writes about the results. It is deliberately never given the
/// raw referral document or the rules engine's source code; it receives
/// only the patient summary and the finalized findings list, so there is
/// no way for it to introduce a claim that isn't already backed by a
/// deterministic finding sitting right next to the text it wrote.
/// </summary>
public sealed class ReviewAgent
{
    private const string Instructions = """
        You are a clinical review assistant drafting a short narrative
        summary for a pharmacist or intake nurse, based on a list of
        findings that have ALREADY been computed by a separate,
        deterministic rules engine. You did not compute these findings
        and cannot verify them independently — your only job is to
        explain them clearly.

        You must NOT:
        - State or imply any finding, risk, or safety judgment that is
          not already present in the findings list you were given.
        - Change, soften, or omit the severity of any finding.
        - Perform any calculation.

        Write two to four short paragraphs in plain, professional
        language suitable for a pharmacist skimming between patients.
        Group related findings together. If there are no findings at all,
        say so plainly and note that this referral did not trigger any
        automated checks, not that it is "safe" (that judgment is the
        reviewer's, not yours).
        """;

    private readonly AIAgent _agent;
    private readonly IAuditLog _auditLog;

    public ReviewAgent(IChatClient chatClient, IAuditLog auditLog)
    {
        _agent = chatClient.AsAIAgent(instructions: Instructions, name: "referral-review-agent");
        _auditLog = auditLog;
    }

    public async Task<string> DraftNarrativeAsync(
        string referralId,
        PatientDemographics patient,
        IReadOnlyList<Finding> findings,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(patient, findings);

        await _auditLog.RecordAsync(referralId, "review.narrative.requested", _agent.Name ?? "referral-review-agent",
            content: prompt, cancellationToken: cancellationToken);

        var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);

        await _auditLog.RecordAsync(referralId, "review.narrative.completed", _agent.Name ?? "referral-review-agent",
            content: response.Text, cancellationToken: cancellationToken);

        return response.Text;
    }

    private static string BuildPrompt(PatientDemographics patient, IReadOnlyList<Finding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Patient: {patient.DisplayName}, age {patient.AgeYears}, referred from {patient.ReferringService}.");
        sb.AppendLine();

        if (findings.Count == 0)
        {
            sb.AppendLine("Findings: none. No automated checks were triggered by this referral.");
            return sb.ToString();
        }

        sb.AppendLine("Findings (already computed — describe these, do not add to or reinterpret them):");
        foreach (var finding in findings)
        {
            sb.AppendLine($"- [{finding.Severity}] {finding.Category}: {finding.Description}");
        }

        return sb.ToString();
    }
}
