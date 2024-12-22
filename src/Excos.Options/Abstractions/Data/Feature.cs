// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections.ObjectModel;

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// Description of a feature.
/// </summary>
public class Feature : KeyedCollection<string, Variant>
{
    /// <summary>
    /// Name of the feature.
    /// </summary>
    public required string Name { get; set; }

    /// <inheritdoc/>
    protected override string GetKeyForItem(Variant item) => item.Id;
}
