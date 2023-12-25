// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections.ObjectModel;

namespace Excos.Options.Abstractions.Data;

public class FeatureCollection : KeyedCollection<string, Feature>
{
    // Feature names should be unique per provider which owns a collection
    protected override string GetKeyForItem(Feature item) => item.Name;
}
