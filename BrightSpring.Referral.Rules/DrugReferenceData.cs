namespace BrightSpring.Referral.Rules;

/// <summary>
/// Synthetic drug reference tables used by the rules engine.
///
/// IMPORTANT — read this before treating any number here as real:
/// this data is intentionally small, invented, and NOT clinically
/// validated. It exists to make the deterministic-rules architecture
/// demonstrable with realistic-looking inputs, not to be a safe source
/// of dosing or interaction truth. A production version of this project
/// would replace this file with a licensed drug database (e.g. First
/// Databank, Medi-Span, or RxNorm plus a licensed interaction/dosing
/// feed) behind the same <see cref="Rules.IReferralRule"/> interface —
/// the rules themselves would not need to change, only where they get
/// their reference values from. That swap-ability is exactly why the
/// reference data is isolated in its own file instead of being inlined
/// into each rule.
/// </summary>
public static class DrugReferenceData
{
    /// <summary>
    /// Maps a medication name (lowercase) to a therapeutic class. Used by
    /// <see cref="DuplicateTherapyRule"/> to catch two different brand or
    /// generic names that treat the same thing — a classic medication
    /// reconciliation gap when a patient arrives with meds listed by
    /// multiple prescribers under different names.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> TherapeuticClass =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lisinopril"] = "ACE inhibitor",
            ["enalapril"] = "ACE inhibitor",
            ["losartan"] = "ARB",
            ["valsartan"] = "ARB",
            ["metoprolol"] = "beta blocker",
            ["atenolol"] = "beta blocker",
            ["simvastatin"] = "statin",
            ["atorvastatin"] = "statin",
            ["omeprazole"] = "proton pump inhibitor",
            ["pantoprazole"] = "proton pump inhibitor",
            ["sertraline"] = "SSRI",
            ["escitalopram"] = "SSRI",
            ["warfarin"] = "anticoagulant",
            ["apixaban"] = "anticoagulant",
            ["aspirin"] = "antiplatelet",
            ["clopidogrel"] = "antiplatelet",
            ["ibuprofen"] = "NSAID",
            ["naproxen"] = "NSAID",
            ["morphine"] = "opioid",
            ["oxycodone"] = "opioid",
            ["hydrocodone"] = "opioid",
            ["furosemide"] = "loop diuretic",
            ["bumetanide"] = "loop diuretic",
        };

    /// <summary>
    /// Maximum reasonable total daily dose in milligrams, used by
    /// <see cref="DoseRangeRule"/>. Deliberately conservative,
    /// single-value ceilings — a real reference table would vary by
    /// indication, renal function, and age band, which is exactly the
    /// kind of nuance this synthetic table skips.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, double> MaxDailyDoseMg =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["lisinopril"] = 40,
            ["metoprolol"] = 400,
            ["simvastatin"] = 80,
            ["omeprazole"] = 40,
            ["sertraline"] = 200,
            ["warfarin"] = 10,
            ["ibuprofen"] = 3200,
            ["morphine"] = 200,
            ["oxycodone"] = 120,
            ["hydrocodone"] = 60,
            ["furosemide"] = 600,
            ["acetaminophen"] = 4000,
        };

    /// <summary>
    /// Substance-to-drug-class cross-reference used by
    /// <see cref="AllergyConflictRule"/> so a documented allergy to a
    /// class ("penicillins") flags a specific drug in that class
    /// ("amoxicillin"), not only an exact name match.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> AllergyClassMembers =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["penicillin"] = new[] { "amoxicillin", "ampicillin", "penicillin", "piperacillin" },
            ["penicillins"] = new[] { "amoxicillin", "ampicillin", "penicillin", "piperacillin" },
            ["sulfa"] = new[] { "sulfamethoxazole", "sulfasalazine", "sulfamethoxazole-trimethoprim" },
            ["sulfa drugs"] = new[] { "sulfamethoxazole", "sulfasalazine", "sulfamethoxazole-trimethoprim" },
            ["nsaids"] = new[] { "ibuprofen", "naproxen", "aspirin" },
            ["codeine"] = new[] { "codeine", "hydrocodone" },
        };

    /// <summary>
    /// Medication name pairs that carry a well-known, high-severity
    /// interaction risk, used by <see cref="HighRiskCombinationRule"/>.
    /// Stored as an unordered pair; the rule checks both directions.
    /// </summary>
    public static readonly IReadOnlyList<(string A, string B, string RiskDescription)> HighRiskPairs = new[]
    {
        ("warfarin", "aspirin", "combined anticoagulant/antiplatelet effect significantly increases bleeding risk"),
        ("warfarin", "ibuprofen", "NSAID use with warfarin increases GI bleeding risk"),
        ("apixaban", "aspirin", "combined anticoagulant/antiplatelet effect significantly increases bleeding risk"),
        ("sertraline", "warfarin", "SSRIs can potentiate warfarin's anticoagulant effect"),
        ("morphine", "oxycodone", "concurrent long-acting and short-acting opioids increase respiratory depression risk without clear indication"),
    };
}
