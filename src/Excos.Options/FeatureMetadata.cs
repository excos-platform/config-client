// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options;

public class FeatureMetadata
{
    public List<FeatureMetadataItem> Features { get; } = new();
}

public class FeatureMetadataItem
{
    public required string FeatureName { get; set; }
    public required string FeatureProvider { get; set; }
    public required string VariantId { get; set; }
    public bool IsOverridden { get; set; }
    public string? OverrideProviderName { get; set; }
}
