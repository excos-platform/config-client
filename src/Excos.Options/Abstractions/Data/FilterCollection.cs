// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections.ObjectModel;

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// Keyed collection of filters, indexed by their property names.
/// </summary>
public class FilterCollection : KeyedCollection<string, Filter>
{
    /// <inheritdoc />
    protected override string GetKeyForItem(Filter item) => item.PropertyName;
}
