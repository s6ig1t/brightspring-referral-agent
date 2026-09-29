using System.Text.RegularExpressions;

namespace BrightSpring.Referral.Agents;

/// <summary>
/// Strips or masks common PHI-shaped identifiers from text before it is
/// written to logs, telemetry, or the audit trail.
///
/// This is not a substitute for real de-identification (see HIPAA's
/// Safe Harbor / Expert Determination standards) — it is a pragmatic,
/// defense-in-depth pass that catches the identifiers most likely to
/// leak into a log line by accident: dates of birth, phone numbers,
/// SSNs, and MRNs. The referral document text itself and any PHI-bearing
/// fields on <see cref="Core.PatientDemographics"/> are never logged in
/// this project regardless — this class is the safety net for anything
/// that does get logged (e.g. an error message that happens to include a
/// snippet of model output).
/// </summary>
public static partial class PhiRedactor
{
    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b")]
    private static partial Regex SsnPattern();

    [GeneratedRegex(@"\b\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}\b")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b(0[1-9]|1[0-2])[/-](0[1-9]|[12]\d|3[01])[/-](19|20)\d{2}\b")]
    private static partial Regex DateOfBirthPattern();

    [GeneratedRegex(@"\bMRN[\s:#-]*\d{5,10}\b", RegexOptions.IgnoreCase)]
    private static partial Regex MrnPattern();

    /// <summary>
    /// Returns a copy of <paramref name="text"/> with recognizable PHI
    /// patterns replaced by a labeled placeholder (e.g. "[REDACTED-SSN]"),
    /// so a reviewer scanning a log can still see that something was
    /// there and what kind of thing it was, without seeing the value.
    /// </summary>
    public static string Redact(string text)
    {
        var redacted = SsnPattern().Replace(text, "[REDACTED-SSN]");
        redacted = MrnPattern().Replace(redacted, "[REDACTED-MRN]");
        redacted = DateOfBirthPattern().Replace(redacted, "[REDACTED-DOB]");
        redacted = PhonePattern().Replace(redacted, "[REDACTED-PHONE]");
        return redacted;
    }
}
