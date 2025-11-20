using OutSystems.ExternalLibraries.SDK;

namespace UnicodeSpoofGuard.Structures;

[OSStructure(Description = "Advanced configuration flags that tailor spoof detection behaviour per call.")]
public struct AnalysisOptions
{
    [OSStructureField(Description = "Run detection in strict mode to surface ASCII and identical homoglyphs.")]
    public bool UseStrictMode;

    [OSStructureField(Description = "Optional locale hint (IETF language tag) needed to resolve locale-aware policy profiles.", IsMandatory = false)]
    public string? Locale;

    [OSStructureField(Description = "Specific policy profile override when multiple policies are defined.", IsMandatory = false)]
    public string? PolicyProfile;

    [OSStructureField(Description = "Set to true to suppress threat intelligence matches for the current request.")]
    public bool DisableThreatIntel;
}

