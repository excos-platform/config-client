// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options;

public class ExperimentMetadata
{
    public List<ExperimentMetadataItem> Experiments { get; } = new();
}

public class ExperimentMetadataItem
{
    public required string ExperimentName { get; set; }
    public required string ExperimentProvider { get; set; }
    public required string VariantId { get; set; }
    public bool IsOverridden { get; set; }
    public string? OverrideProviderName { get; set; }
}
