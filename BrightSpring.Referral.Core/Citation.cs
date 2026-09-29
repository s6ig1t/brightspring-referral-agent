namespace BrightSpring.Referral.Core;

/// <summary>
/// A pointer back to the exact spot in the source referral document that a
/// piece of extracted data came from.
///
/// This is the mechanism behind the project's core trust claim: every
/// clinical fact the pharmacist sees in a review brief can be traced back
/// to a page and a snippet of the original document, rather than taking
/// the model's word for it. A citation is cheap to produce and expensive
/// to fake convincingly, which is exactly the property you want when the
/// downstream reader is deciding whether to trust an AI-assisted summary
/// of someone's medication list.
/// </summary>
/// <param name="SourceDocument">
/// File name (or logical name) of the referral document this fact came
/// from. Referral packets in this project are a single document, but the
/// field exists because production packets often bundle a discharge
/// summary, a med list, and physician orders as separate files.
/// </param>
/// <param name="PageNumber">
/// 1-based page number within the source document. Sample documents in
/// this project use "--- Page N ---" markers so a plain-text extraction
/// agent can report a real page number without needing PDF layout
/// analysis.
/// </param>
/// <param name="Snippet">
/// The literal text the fact was extracted from, verbatim. Kept short —
/// this is for a reviewer to visually confirm the extraction, not a full
/// paragraph.
/// </param>
public sealed record Citation(string SourceDocument, int PageNumber, string Snippet)
{
    public override string ToString() => $"{SourceDocument}, p.{PageNumber}: \"{Snippet}\"";
}
