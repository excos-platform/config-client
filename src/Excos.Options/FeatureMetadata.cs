// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;

namespace Excos.Options;

/// <summary>
/// Metadata about the features applicable to the current context.
/// </summary>
public class FeatureMetadata
{
    /// <summary>
    /// Collection of per feature metadata.
    /// </summary>
    public ICollection<FeatureMetadataItem> Features { get; init; } = new List<FeatureMetadataItem>(capacity: 8);
}

/// <summary>
/// Feature metadata.
/// </summary>
public class FeatureMetadataItem
{
    /// <summary>
    /// Name of the feature.
    /// </summary>
    public required string FeatureName { get; set; }

    /// <summary>
    /// Name of the provider which provided the feature.
    /// </summary>
    public required string FeatureProvider { get; set; }

    /// <summary>
    /// Id of the feature variant applied to the configuration.
    /// </summary>
    public required string VariantId { get; set; }

    /// <summary>
    /// Whether the variant has been overridden by an <see cref="IFeatureVariantOverride"/> provider.
    /// </summary>
    public bool IsOverridden { get; set; }

    /// <summary>
    /// Name of the provider which provided the override (if <see cref="IsOverridden"/> is true).
    /// </summary>
    public string? OverrideProviderName { get; set; }
}
