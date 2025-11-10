using OutSystems.ExternalLibraries.SDK;

namespace UnicodeSpoofGuard.Structures;

[OSStructure(Description = "Single suspicious element detected during Unicode spoof analysis.")]
public struct DetailedFinding
{
    [OSStructureField(Description = "Category of the finding, such as Homoglyph or MixedScript.")]
    public string FindingType;
    
    [OSStructureField(Description = "Human-readable explanation of why this portion of text was flagged.")]
    public string Description;
    
    [OSStructureField(Description = "Zero-based character index within the original input where the finding begins.")]
    public int Position;
    
    [OSStructureField(Description = "Exact substring from the original input that triggered the finding.")]
    public string Substring;
}
