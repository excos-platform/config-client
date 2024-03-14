// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections.ObjectModel;

namespace Excos.Options.Utils
{
    internal static class KeyedCollectionExtensions
    {
        public static void AddRange<U,T>(this KeyedCollection<U, T> collection, IEnumerable<T> items)
            where T : notnull
            where U : notnull
        {
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }
    }
}
