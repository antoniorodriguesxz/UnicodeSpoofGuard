using System;
using OutSystems.ExternalLibraries.SDK;

namespace UnicodeSpoofGuard.Structures;

[OSStructure(Description = "Result of attempting to refresh the Unicode confusables dataset.")]
public struct RefreshConfusablesResult
{
    [OSStructureField(Description = "True when the dataset refresh completed successfully.")]
    public bool IsSuccess;

    [OSStructureField(Description = "Diagnostic message describing the outcome.")]
    public string Message;

    [OSStructureField(Description = "Version string reported by the source confusables dataset.", IsMandatory = false)]
    public string? SourceVersion;

    [OSStructureField(Description = "Original publication date reported by the dataset.", IsMandatory = false)]
    public DateTimeOffset? SourceDate;

    [OSStructureField(Description = "Timestamp when the dataset was retrieved.", IsMandatory = false)]
    public DateTimeOffset? RetrievedAt;

    [OSStructureField(Description = "Number of confusable mappings made available after the refresh.", IsMandatory = false)]
    public int? EntryCount;
}

