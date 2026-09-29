using System.Text.Json.Serialization;
using BrightSpring.Referral.Agents;
using BrightSpring.Referral.Rules;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ---------------------------------------------------
// Locally: `dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."` from
// inside the Api project folder. Secrets set this way are stored outside
// the repo (see UserSecretsId in the .csproj) and are picked up
// automatically by AddUserSecrets below in the Development environment.
//
// In any real environment (staging, production): the same "Claude:ApiKey"
// configuration key is instead supplied by a secrets manager or injected
// by CI/CD as an environment variable (ASP.NET Core config maps
// "Claude__ApiKey" to "Claude:ApiKey" automatically). No code change is
// needed to move between the two — only where the value comes from
// changes.
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.Configure<ClaudeChatClientOptions>(
    builder.Configuration.GetSection(ClaudeChatClientOptions.SectionName));

// ---- Core services ------------------------------------------------------

// IChatClient is the seam: everything above this line in the Agents
// project (IntakeAgent, ReviewAgent) is written against IChatClient, not
// against Claude. Swapping this one registration for a different
// IChatClient implementation is the entire migration path to a different
// model provider.
builder.Services.AddHttpClient<ClaudeChatClient>();
builder.Services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<ClaudeChatClient>());

builder.Services.AddSingleton<IAuditLog, ConsoleAuditLog>();
builder.Services.AddSingleton<IReviewBriefStore, InMemoryReviewBriefStore>();
builder.Services.AddSingleton<ReferralRuleEngine>();
builder.Services.AddSingleton<IntakeAgent>();
builder.Services.AddSingleton<ReviewAgent>();
builder.Services.AddSingleton<ReferralIntakeOrchestrator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// FindingSeverity, FindingCategory, and ApprovalStatus are all enums.
// System.Text.Json serializes an enum as its underlying number by
// default — 2, not "Critical" — which the demo page's JS (and any real
// client) would have to translate back by hand. Serializing them as
// their name instead keeps the wire format self-describing and matches
// what wwwroot/index.html expects when it reads finding.severity.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// Serves wwwroot/index.html at "/" (UseDefaultFiles) and everything else
// under wwwroot — including wwwroot/samples/*.txt, which the demo page
// fetches directly — as static files. This is the same static-page-in-
// front-of-the-API pattern the aloan.ai demo used.
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ---- Endpoints ---------------------------------------------------------
// Deliberately a thin, minimal-API surface: every endpoint below is a
// one-line delegation into ReferralIntakeOrchestrator or
// IReviewBriefStore. There is no business logic here to keep in sync
// with the Agents/Rules projects — that is the point of pushing all of
// it down into those layers.

app.MapPost("/referrals/intake", async (IntakeRequest request, ReferralIntakeOrchestrator orchestrator, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.DocumentText))
    {
        return Results.BadRequest("documentText is required.");
    }

    var brief = await orchestrator.ProcessReferralAsync(
        request.SourceDocumentName ?? "unnamed-referral.txt", request.DocumentText, ct);

    return Results.Created($"/referrals/{brief.BriefId}", brief);
})
.WithName("SubmitReferral")
.WithSummary("Runs a raw referral document through extraction, rules evaluation, and narrative drafting; returns a brief pending pharmacist review.");

app.MapGet("/referrals/{briefId}", async (string briefId, IReviewBriefStore store, CancellationToken ct) =>
{
    var brief = await store.GetAsync(briefId, ct);
    return brief is null ? Results.NotFound() : Results.Ok(brief);
})
.WithName("GetReviewBrief");

app.MapGet("/referrals", async (IReviewBriefStore store, CancellationToken ct) =>
    Results.Ok(await store.ListAsync(ct)))
.WithName("ListReviewBriefs");

app.MapPost("/referrals/{briefId}/approve", async (string briefId, ReviewDecisionRequest request, IReviewBriefStore store, CancellationToken ct) =>
{
    // The reviewer name is required, never defaulted: an approval with
    // no named accountable person is not a real approval in a clinical
    // workflow.
    if (string.IsNullOrWhiteSpace(request.ReviewerName))
    {
        return Results.BadRequest("reviewerName is required.");
    }

    try
    {
        var updated = await store.ApproveAsync(briefId, request.ReviewerName, request.Notes, ct);
        return Results.Ok(updated);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(ex.Message);
    }
})
.WithName("ApproveReviewBrief");

app.MapPost("/referrals/{briefId}/reject", async (string briefId, ReviewDecisionRequest request, IReviewBriefStore store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.ReviewerName))
    {
        return Results.BadRequest("reviewerName is required.");
    }

    try
    {
        var updated = await store.RejectAsync(briefId, request.ReviewerName, request.Notes, ct);
        return Results.Ok(updated);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(ex.Message);
    }
})
.WithName("RejectReviewBrief");

app.Run();

internal sealed record IntakeRequest(string? SourceDocumentName, string DocumentText);

internal sealed record ReviewDecisionRequest(string ReviewerName, string? Notes);
