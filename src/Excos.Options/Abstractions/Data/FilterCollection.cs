// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections.ObjectModel;

namespace Excos.Options.Abstractions.Data;

public class FilterCollection : KeyedCollection<string, Filter>
{
    protected override string GetKeyForItem(Filter item) => item.PropertyName;

    public void AddRange(IEnumerable<Filter> filters)
    {
        foreach (var filter in filters)
        {
            Add(filter);
        }
    }
}
