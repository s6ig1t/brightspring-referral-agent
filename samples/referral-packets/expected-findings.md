# What each sample packet should trigger

These notes describe the findings the deterministic rules engine is
expected to produce for each sample referral, so you can sanity-check a
run without having to work it out from scratch. They describe the
*rules engine's* output, which is independent of the extraction agent —
if the agent extracts a medication name or dose slightly differently
than expected, the finding may not fire, and that's a useful signal
about the extraction prompt, not about the rules engine.

## home-health-referral-01.txt (Eleanor Whitfield)

- **Allergy conflict (Critical):** documented penicillin allergy vs.
  amoxicillin 500mg — a class-level match, not an exact name match.
- **Duplicate therapy (Caution):** lisinopril and enalapril are both
  ACE inhibitors.
- **High-risk combination (Critical):** warfarin + aspirin — combined
  anticoagulant/antiplatelet bleeding risk.

## hospice-referral-02.txt (Harold Jennings)

- **High-risk combination (Critical):** morphine + oxycodone — a
  long-acting and short-acting opioid present together.
- **High-risk combination (Critical):** sertraline + warfarin would
  also fire *if* warfarin were on this list — it isn't in this sample,
  included here only as a reminder that the pair is directional-agnostic.
  (Not expected to fire for this packet as written.)
- No allergy conflicts expected (no known drug allergies documented).

## behavioral-health-pharmacy-referral-03.txt (Dana Ruiz)

- **Allergy conflict (Critical):** documented sulfa allergy vs.
  sulfamethoxazole-trimethoprim — a class-level match.
- **Dose out of range (Critical):** lisinopril 80mg exceeds the
  reference maximum daily dose of 40mg.

None of these three packets should trigger every rule — that's
intentional. A referral pipeline that flags everything is exactly as
useless to a pharmacist as one that flags nothing; the point of these
samples is a realistic mix of clean fields and real issues, the same
mix a reviewer sees in a normal day.
