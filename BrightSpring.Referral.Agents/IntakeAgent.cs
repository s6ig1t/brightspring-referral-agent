using System.Text.Json;
using System.Text.Json.Serialization;
using BrightSpring.Referral.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace BrightSpring.Referral.Agents;

/// <summary>
/// Reads a raw referral document (plain text, with "--- Page N ---"
/// markers) and produces a fully structured, cited <see cref="ReferralPacket"/>.
///
/// This is the only place in the pipeline where an LLM reads the source
/// document. It is instructed, explicitly and repeatedly in its system
/// prompt, to extract facts with citations and never to calculate, infer,
/// or editorialize about clinical safety — that job belongs entirely to
/// BrightSpring.Referral.Rules, which this agent has no reference to and
/// no awareness of.
///
/// Built on Microsoft.Agents.AI's <c>AsAIAgent</c> extension over an
/// <see cref="IChatClient"/>: the agent object itself only knows about
/// the abstraction, so whichever <see cref="IChatClient"/> is registered
/// in DI (Claude today, potentially Azure OpenAI tomorrow) is what
/// actually serves the extraction.
/// </summary>
public sealed class IntakeAgent
{
    private const string Instructions = """
        You are a clinical intake extraction assistant for a home- and
        community-based healthcare referral pipeline. You will be given
        the full text of a referral document, with page boundaries marked
        as "--- Page N ---".

        Your ONLY job is extraction. You must:
        - Extract patient display name, age in years, and referring service.
        - Extract every medication mentioned, with its dose amount, dose
          unit, frequency, and route, exactly as documented.
        - Extract every documented allergy and its reaction, if stated.
        - Extract every diagnosis or problem-list entry mentioned.
        - For every single field you extract, report the page number it
          appeared on and the exact snippet of text (a short phrase, not
          a whole paragraph) that the value came from.

        You must NOT:
        - Calculate anything (no dose totals, no ratios, no risk scores).
        - Decide whether a dose, combination, or allergy is safe or unsafe.
        - Infer a value that is not explicitly stated in the document.
        - Add commentary, warnings, or recommendations of any kind.

        If a field is not present in the document, omit it rather than
        guessing. Respond with ONLY a single JSON object matching the
        schema you have been given — no prose before or after it.
        """;

    private readonly AIAgent _agent;
    private readonly IAuditLog _auditLog;

    public IntakeAgent(IChatClient chatClient, IAuditLog auditLog)
    {
        // AsAIAgent wires the chat client, instructions, and a name into
        // a runnable ChatClientAgent (which derives from AIAgent). The
        // framework itself never touches Claude directly — it only ever
        // calls through the IChatClient interface that was handed to it.
        _agent = chatClient.AsAIAgent(instructions: Instructions, name: "referral-intake-agent");
        _auditLog = auditLog;
    }

    /// <summary>
    /// Runs extraction over the given document text and returns a fully
    /// structured, cited packet. Throws if the model's output cannot be
    /// parsed as the expected schema — this project intentionally fails
    /// loudly rather than silently falling back to an empty packet, since
    /// a silently-empty extraction is far more dangerous than a visible
    /// error in a clinical intake pipeline.
    /// </summary>
    public async Task<ReferralPacket> ExtractAsync(
        string referralId,
        string sourceDocumentName,
        string documentText,
        CancellationToken cancellationToken = default)
    {
        await _auditLog.RecordAsync(referralId, "intake.extraction.requested", _agent.Name ?? "referral-intake-agent",
            content: documentText, cancellationToken: cancellationToken);

        // RunAsync is the single-turn equivalent of RunStreamingAsync —
        // used here because the caller needs the complete JSON payload
        // before it can be parsed, not an incremental token stream.
        var response = await _agent.RunAsync(BuildUserPrompt(documentText), cancellationToken: cancellationToken);

        var extraction = ParseExtraction(response.Text)
            ?? throw new InvalidOperationException(
                $"Intake agent response for referral {referralId} could not be parsed as the expected extraction schema.");

        var packet = MapToPacket(referralId, sourceDocumentName, extraction);

        await _auditLog.RecordAsync(referralId, "intake.extraction.completed", _agent.Name ?? "referral-intake-agent",
            content: response.Text, cancellationToken: cancellationToken);

        return packet;
    }

    // NOTE: this schema block is a plain (non-interpolated) raw string —
    // no leading '$'. Raw string interpolation ties the number of '$'
    // characters to how many consecutive '{' start an interpolation, and
    // JSON's own doubled/nested braces (e.g. an object inside an array,
    // "[ { ... } ]") would collide with that if this were interpolated.
    // Keeping the literal JSON as a separate, non-interpolated constant
    // and only interpolating documentText in a second string (where the
    // only braces are the single-brace {documentText} placeholder) avoids
    // the ambiguity entirely, rather than trying to escape around it.
    private const string ExtractionJsonSchema = """
        Required JSON shape:
        {
          "patient": { "displayName": "", "ageYears": 0, "referringService": "", "sourcePage": 0, "sourceSnippet": "" },
          "medications": [ { "name": "", "doseAmount": 0, "doseUnit": "", "frequency": "", "route": "", "sourcePage": 0, "sourceSnippet": "" } ],
          "allergies": [ { "substance": "", "reaction": null, "sourcePage": 0, "sourceSnippet": "" } ],
          "diagnoses": [ { "description": "", "sourcePage": 0, "sourceSnippet": "" } ]
        }
        """;

    private static string BuildUserPrompt(string documentText) =>
        $"""
        Extract the schema described in your instructions from the
        following referral document. Respond with ONLY the JSON object.

        {ExtractionJsonSchema}

        --- BEGIN REFERRAL DOCUMENT ---
        {documentText}
        --- END REFERRAL DOCUMENT ---
        """;

    private static ExtractionResult? ParseExtraction(string modelResponseText)
    {
        // Models occasionally wrap JSON in a code fence despite
        // instructions not to; strip that defensively rather than
        // failing on an easily-recoverable formatting quirk.
        var trimmed = modelResponseText.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
            {
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
            }
        }

        return JsonSerializer.Deserialize<ExtractionResult>(trimmed, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
    }

    private static ReferralPacket MapToPacket(string referralId, string sourceDocumentName, ExtractionResult extraction)
    {
        Citation Cite(int page, string snippet) => new(sourceDocumentName, page, snippet);

        var patient = new PatientDemographics(
            extraction.Patient.DisplayName,
            extraction.Patient.AgeYears,
            extraction.Patient.ReferringService,
            Cite(extraction.Patient.SourcePage, extraction.Patient.SourceSnippet));

        var medications = (extraction.Medications ?? new List<ExtractedMedication>())
            .Select(m => new Medication(
                m.Name, m.DoseAmount, m.DoseUnit, m.Frequency, m.Route,
                Cite(m.SourcePage, m.SourceSnippet)))
            .ToList();

        var allergies = (extraction.Allergies ?? new List<ExtractedAllergy>())
            .Select(a => new Allergy(a.Substance, a.Reaction, Cite(a.SourcePage, a.SourceSnippet)))
            .ToList();

        var diagnoses = (extraction.Diagnoses ?? new List<ExtractedDiagnosis>())
            .Select(d => new Diagnosis(d.Description, Cite(d.SourcePage, d.SourceSnippet)))
            .ToList();

        return new ReferralPacket(
            referralId, sourceDocumentName, patient, medications, allergies, diagnoses, DateTimeOffset.UtcNow);
    }

    // ---- Extraction DTOs -----------------------------------------------
    // Mirrors the JSON shape requested in the prompt. Kept separate from
    // the Core domain records because this shape is a wire contract with
    // the model, not a domain concept — e.g. it uses raw page/snippet
    // fields instead of a nested Citation, since that is what's simplest
    // for a model to emit reliably.

    private sealed class ExtractionResult
    {
        public ExtractedPatient Patient { get; set; } = new();
        public List<ExtractedMedication>? Medications { get; set; }
        public List<ExtractedAllergy>? Allergies { get; set; }
        public List<ExtractedDiagnosis>? Diagnoses { get; set; }
    }

    private sealed class ExtractedPatient
    {
        public string DisplayName { get; set; } = string.Empty;
        public int AgeYears { get; set; }
        public string ReferringService { get; set; } = string.Empty;
        public int SourcePage { get; set; } = 1;
        public string SourceSnippet { get; set; } = string.Empty;
    }

    private sealed class ExtractedMedication
    {
        public string Name { get; set; } = string.Empty;
        public double DoseAmount { get; set; }
        public string DoseUnit { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public int SourcePage { get; set; } = 1;
        public string SourceSnippet { get; set; } = string.Empty;
    }

    private sealed class ExtractedAllergy
    {
        public string Substance { get; set; } = string.Empty;
        public string? Reaction { get; set; }
        public int SourcePage { get; set; } = 1;
        public string SourceSnippet { get; set; } = string.Empty;
    }

    private sealed class ExtractedDiagnosis
    {
        public string Description { get; set; } = string.Empty;
        public int SourcePage { get; set; } = 1;
        public string SourceSnippet { get; set; } = string.Empty;
    }
}
