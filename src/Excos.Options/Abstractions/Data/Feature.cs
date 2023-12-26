// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// Description of a feature.
/// </summary>
public class Feature
{
    /// <summary>
    /// Name of the feature.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Name of the feature provider.
    /// This is used for metadata to help determine the source of feature configuration.
    /// </summary>
    public required string ProviderName { get; set; }

    /// <summary>
    /// Whether the feature is active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Collection of filters which will be checked against a user provided context.
    /// </summary>
    public FilterCollection Filters { get; } = new();

    /// <summary>
    /// Collection of variants for this feature.
    /// </summary>
    public VariantCollection Variants { get; } = new();

    /// <summary>
    /// Salt used for variant allocation calculations.
    /// By default it's just the name of the provider and feature.
    /// </summary>
    public string Salt
    {
        get
        {
            if (_salt == null)
            {
                // IMPORTANT: do not change this going forward
                // as it will break any experiments using the default salt
                _salt = $"{ProviderName}_{Name}";
            }

            return _salt;
        }
        set => _salt = value;
    }

    private string? _salt = null;
}
