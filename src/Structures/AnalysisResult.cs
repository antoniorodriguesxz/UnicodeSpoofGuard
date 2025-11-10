using OutSystems.ExternalLibraries.SDK;

namespace UnicodeSpoofGuard.Structures;

[OSStructure(Description = "Outcome of a spoofing assessment performed by Unicode Spoof Guard.")]
public struct AnalysisResult
{
    [OSStructureField(Description = "Indicates whether the analyzed input passed all spoofing checks.")]
    public bool IsSafe;

    [OSStructureField(Description = "Concise explanation when IsSafe is false; empty when no issues were found.")]
    public string Reason;

    [OSStructureField(Description = "Canonicalized version of the analyzed text using trusted replacements.")]
    public string CanonicalValue;

    [OSStructureField(Description = "Detailed list of every finding detected during analysis.")] 
    public List<DetailedFinding> Findings;
}
