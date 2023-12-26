// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections.ObjectModel;

namespace Excos.Options.Abstractions.Data;

public class VariantCollection : KeyedCollection<string, Variant>
{
    protected override string GetKeyForItem(Variant item) => item.Id;

    public void AddRange(IEnumerable<Variant> variants)
    {
        foreach (var variant in variants)
        {
            Add(variant);
        }
    }
}
