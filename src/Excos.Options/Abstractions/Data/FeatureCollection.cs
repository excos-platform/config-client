// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections.ObjectModel;

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// Collection of features, indexed by their names.
/// </summary>
/// <remarks>
/// Currently used mainly for Options based configuration.
/// </remarks>
public class FeatureCollection : KeyedCollection<string, Feature>
{
    /// <inheritdoc />
    /// <remarks>
    /// Feature names should be unique per provider which owns a collection.
    /// </remarks>
    protected override string GetKeyForItem(Feature item) => item.Name;
}
