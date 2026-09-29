using System.Security.Cryptography;
using System.Text;

namespace BrightSpring.Referral.Agents;

/// <summary>
/// A single audit trail entry: what happened, when, to which referral,
/// and — critically — never the PHI itself. Every model call and every
/// human approval decision produces one of these.
/// </summary>
public sealed record AuditLogEntry(
    string ReferralId,
    string EventType,
    string ActorName,
    string InputContentHash,
    DateTimeOffset TimestampUtc,
    string? Notes = null);

/// <summary>
/// Records audit entries for every AI call and every reviewer decision in
/// the pipeline.
///
/// The design choice worth calling out: this interface takes raw content
/// and hashes it internally (<see cref="ComputeHash"/>), rather than
/// asking every caller to hash it themselves and pass a string. That
/// means a caller cannot accidentally log raw PHI by forgetting a
/// redaction step — the only thing that ever leaves this class as a
/// stored value is a SHA-256 hash, useful for proving "this exact input
/// was processed at this exact time" without the log itself becoming a
/// second copy of the patient's data.
/// </summary>
public interface IAuditLog
{
    Task RecordAsync(string referralId, string eventType, string actorName, string content, string? notes = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Console-backed audit log for the demo. A production deployment would
/// write these entries to an append-only store (a database table with no
/// update/delete grants, or a managed audit log service) rather than
/// stdout — the interface is what matters for swapping that in, not this
/// implementation.
/// </summary>
public sealed class ConsoleAuditLog : IAuditLog
{
    public Task RecordAsync(
        string referralId,
        string eventType,
        string actorName,
        string content,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditLogEntry(
            ReferralId: referralId,
            EventType: eventType,
            ActorName: actorName,
            InputContentHash: ComputeHash(content),
            TimestampUtc: DateTimeOffset.UtcNow,
            Notes: notes is null ? null : PhiRedactor.Redact(notes));

        Console.WriteLine(
            $"[audit] {entry.TimestampUtc:O} referral={entry.ReferralId} event={entry.EventType} " +
            $"actor={entry.ActorName} inputHash={entry.InputContentHash}" +
            (entry.Notes is null ? "" : $" notes=\"{entry.Notes}\""));

        return Task.CompletedTask;
    }

    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
