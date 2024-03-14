// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections.ObjectModel;

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// Keyed collection of variants, indexed by their IDs.
/// </summary>
public class VariantCollection : KeyedCollection<string, Variant>
{
    /// <inheritdoc />
    protected override string GetKeyForItem(Variant item) => item.Id;
}
