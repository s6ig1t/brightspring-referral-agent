# BrightSpring Referral Intake & Review Pipeline

An agentic pipeline that reads a home-health, hospice, or behavioral-health
referral document, checks it against deterministic clinical rules, and
drafts a cited review brief for a pharmacist or intake nurse — where every
finding traces back to the exact page and snippet it came from, and
nothing is finalized without a named human's approval.

Built as a hands-on portfolio project applying agentic AI development —
the Microsoft Agent Framework (`Microsoft.Agents.AI`), Claude, and
.NET 10 — to a realistic home- and community-based healthcare workflow,
in the style of home health, hospice, and pharmacy coordination services
that companies like BrightSpring Health Services operate at scale.

**This project is independent, unaffiliated portfolio work.** It is not
built with, endorsed by, or reviewed by BrightSpring Health Services or
any other company. All referral documents in `samples/` are entirely
synthetic — invented names, invented conditions, invented medication
lists — and the drug reference data in
`BrightSpring.Referral.Rules/DrugReferenceData.cs` is a small, self-authored
table for demonstration purposes, **not clinically validated data**. See
that file's header comment for what a production version would replace
it with.

## The core design rule

**The model reads the document and writes the narrative. It never touches the clinical judgment.**

Every finding — a duplicate drug class, a dose above reference range, an
allergy conflict, a high-risk combination — is computed by plain,
deterministic, unit-tested C#. The language model is never asked to
decide whether something is safe, and is never shown the raw document
when drafting the review narrative; it only ever sees already-computed,
already-correct findings. That separation isn't just a design intention —
it's enforced by the project structure itself: `BrightSpring.Referral.Rules`
has zero dependency on anything AI-related, and could not call into an
LLM even if a future contributor tried to add that.

The second rule, just as load-bearing for a healthcare context: **nothing
finalizes itself.** A generated review brief always starts in
`PendingReview`. The only way it becomes `Approved` or `Rejected` is a
named reviewer calling an endpoint — there is no code path anywhere in
this solution that transitions a brief automatically.

## Architecture

```
BrightSpring.Referral.Core         Plain domain models. No logic, no dependencies.
BrightSpring.Referral.Rules        Deterministic rules engine + reference data.
                                    No AI, no HTTP — fully unit-testable in isolation.
BrightSpring.Referral.Rules.Tests  xUnit tests for every rule and the engine's aggregation.
BrightSpring.Referral.Agents       The only project that talks to Claude.
                                    IntakeAgent (extraction), ReviewAgent (narrative),
                                    the orchestrator, the PHI redactor, and the audit log.
BrightSpring.Referral.Api          Minimal API: submit a referral, review pending
                                    briefs, approve or reject them.
```

Dependency direction is one-way and enforced by project references, not
just convention: `Rules` depends only on `Core`; `Agents` depends on
`Core` and `Rules` (to call the engine, never the reverse); `Api`
depends on all three and adds nothing but HTTP plumbing.

```
        ┌─────────────────────┐
 raw    │   IntakeAgent        │   structured, cited
 text  →│  (Claude via         │→  ReferralPacket
        │   IChatClient)        │
        └─────────────────────┘
                                        │
                                        ▼
                          ┌─────────────────────────┐
                          │   ReferralRuleEngine      │   plain C#, no AI
                          │  (DuplicateTherapy,        │   ─────────────
                          │   DoseRange, Allergy,      │→  Finding[]
                          │   HighRiskCombination)     │
                          └─────────────────────────┘
                                        │
                                        ▼
                          ┌─────────────────────────┐
 Finding[] +              │   ReviewAgent              │   narrative text,
 patient summary  ───────→│  (Claude via               │→  never sees the
 (no raw document)         │   IChatClient)              │  raw document
                          └─────────────────────────┘
                                        │
                                        ▼
                          ┌─────────────────────────┐
                          │   ReviewBrief              │
                          │   status: PendingReview    │───→ held here until a
                          └─────────────────────────┘      named reviewer
                                        ▲                    approves/rejects
                                        │
                          POST /referrals/{id}/approve
                          POST /referrals/{id}/reject
```

## Provider-agnostic by construction

`Microsoft.Agents.AI` is a backend-agnostic agent framework: it is built
on top of `Microsoft.Extensions.AI`'s `IChatClient` abstraction, and
nothing in `IntakeAgent` or `ReviewAgent` knows or cares which model is
actually answering. `ClaudeChatClient` is the provider-specific piece —
a strongly-typed client connecting to Claude's Messages API — registered
once, in `Program.cs`, as the `IChatClient` implementation the whole
Agents project resolves through dependency injection. Swapping in an
Azure OpenAI or local-model backend later is a single registration
change, not a rewrite of either agent.

## Configuration

The Claude API key is never hardcoded and never committed:

**Locally**, from inside `BrightSpring.Referral.Api`:

```
dotnet user-secrets init
dotnet user-secrets set "Claude:ApiKey" "sk-ant-your-key-here"
```

**In any real environment**, the same `Claude:ApiKey` configuration key
is instead supplied by a secrets manager (Azure Key Vault, AWS Secrets
Manager) or injected by CI/CD as an environment variable
(`Claude__ApiKey`) — no code change is required to move between the two.

## Running it

```
dotnet build
dotnet test                          # runs BrightSpring.Referral.Rules.Tests
dotnet run --project BrightSpring.Referral.Api
```

Once the API is running, submit one of the sample referrals:

```
curl -X POST https://localhost:7180/referrals/intake \
  -H "Content-Type: application/json" \
  -d "{\"sourceDocumentName\":\"home-health-referral-01.txt\",\"documentText\":\"$(cat samples/referral-packets/home-health-referral-01.txt | sed 's/"/\\"/g' | tr '\n' ' ')\"}"
```

Or open `/openapi` (Development environment) for the full endpoint list
and try it from Swagger/OpenAPI UI. See
`samples/referral-packets/expected-findings.md` for what each sample
document should trigger, so you can sanity-check a run.

## PHI handling and audit trail

Two things worth calling out for a healthcare-adjacent demo:

- **`PhiRedactor`** strips common PHI-shaped patterns (SSNs, phone
  numbers, dates of birth, MRNs) from anything that reaches a log or
  audit note. It's a defense-in-depth net, not a substitute for the fact
  that raw referral text and patient fields are never logged directly
  anywhere in this codebase.
- **`IAuditLog`** records every model call and every reviewer decision —
  but only a SHA-256 hash of the input content, a timestamp, and an
  actor name. The audit trail can prove "this exact input was processed
  by this agent at this time" without becoming a second, unprotected copy
  of the patient's data.

## What a production version would add

- A licensed drug reference/interaction database behind
  `IReferralRule` and `DrugReferenceData`, instead of the small synthetic
  tables here.
- A real datastore behind `IReviewBriefStore`, so a pending review
  survives a restart, plus role-based access control on the approve/reject
  endpoints.
- The human-in-the-loop gate expressed as a Microsoft.Agents.AI workflow
  request/response node with built-in checkpointing, rather than the
  explicit orchestrator method in `ReferralIntakeOrchestrator`.
- OpenTelemetry tracing across the pipeline steps, and a small evaluation
  set to regression-test extraction accuracy and citation correctness
  whenever the intake prompt changes.
- Document intake beyond plain text — PDF and scanned-fax parsing, given
  how referrals actually arrive in this industry.
