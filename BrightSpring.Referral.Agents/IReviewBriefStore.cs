using System.Collections.Concurrent;
using BrightSpring.Referral.Core;

namespace BrightSpring.Referral.Agents;

/// <summary>
/// Persistence for review briefs across the pending-review → approved/
/// rejected lifecycle.
///
/// This is where the human-in-the-loop gate actually lives: a brief is
/// created with <see cref="ApprovalStatus.PendingReview"/> and NOTHING in
/// this codebase ever transitions it out of that state automatically.
/// Only an explicit call to <see cref="ApproveAsync"/> or
/// <see cref="RejectAsync"/> — which in the Api project means a named
/// reviewer hitting an endpoint — changes it.
///
/// In a production system built on Microsoft.Agents.AI's workflow
/// support, this same gate would more naturally be modeled as a workflow
/// pausing at a request/response node and persisting its checkpoint until
/// external input resumes it, rather than a simple store polled by an
/// API. The in-memory store here is the simplest thing that demonstrates
/// the same guarantee — an AI-drafted brief cannot finalize itself —
/// without requiring a database for a demo.
/// </summary>
public interface IReviewBriefStore
{
    Task<ReviewBrief> SaveAsync(ReviewBrief brief, CancellationToken cancellationToken = default);

    Task<ReviewBrief?> GetAsync(string briefId, CancellationToken cancellationToken = default);

    Task<ReviewBrief> ApproveAsync(string briefId, string reviewerName, string? notes, CancellationToken cancellationToken = default);

    Task<ReviewBrief> RejectAsync(string briefId, string reviewerName, string? notes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReviewBrief>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory implementation for the demo API. A production deployment
/// would back this with a real datastore so pending reviews survive a
/// restart — the interface, and the invariant it enforces (no
/// self-approval), is the part worth carrying forward.
/// </summary>
public sealed class InMemoryReviewBriefStore : IReviewBriefStore
{
    private readonly ConcurrentDictionary<string, ReviewBrief> _briefs = new();
    private readonly IAuditLog _auditLog;

    public InMemoryReviewBriefStore(IAuditLog auditLog) => _auditLog = auditLog;

    public Task<ReviewBrief> SaveAsync(ReviewBrief brief, CancellationToken cancellationToken = default)
    {
        _briefs[brief.BriefId] = brief;
        return Task.FromResult(brief);
    }

    public Task<ReviewBrief?> GetAsync(string briefId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_briefs.GetValueOrDefault(briefId));

    public async Task<ReviewBrief> ApproveAsync(string briefId, string reviewerName, string? notes, CancellationToken cancellationToken = default)
    {
        var updated = await TransitionAsync(briefId, ApprovalStatus.Approved, reviewerName, notes, cancellationToken);
        await _auditLog.RecordAsync(updated.ReferralId, "review.approved", reviewerName, content: briefId, notes: notes, cancellationToken: cancellationToken);
        return updated;
    }

    public async Task<ReviewBrief> RejectAsync(string briefId, string reviewerName, string? notes, CancellationToken cancellationToken = default)
    {
        var updated = await TransitionAsync(briefId, ApprovalStatus.Rejected, reviewerName, notes, cancellationToken);
        await _auditLog.RecordAsync(updated.ReferralId, "review.rejected", reviewerName, content: briefId, notes: notes, cancellationToken: cancellationToken);
        return updated;
    }

    public Task<IReadOnlyList<ReviewBrief>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ReviewBrief>>(_briefs.Values.OrderByDescending(b => b.GeneratedAtUtc).ToList());

    private Task<ReviewBrief> TransitionAsync(string briefId, ApprovalStatus newStatus, string reviewerName, string? notes, CancellationToken cancellationToken)
    {
        if (!_briefs.TryGetValue(briefId, out var existing))
        {
            throw new KeyNotFoundException($"No review brief found with id '{briefId}'.");
        }

        if (existing.Status != ApprovalStatus.PendingReview)
        {
            throw new InvalidOperationException(
                $"Review brief '{briefId}' has already been {existing.Status} and cannot be transitioned again.");
        }

        var updated = existing with
        {
            Status = newStatus,
            ReviewedBy = reviewerName,
            ReviewedAtUtc = DateTimeOffset.UtcNow,
            ReviewerNotes = notes,
        };

        _briefs[briefId] = updated;
        return Task.FromResult(updated);
    }
}
