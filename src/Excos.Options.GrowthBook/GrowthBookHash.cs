// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;

namespace Excos.Options.GrowthBook;

internal class GrowthBookHash : IAllocationHash
{
    public static GrowthBookHash V1 { get; } = new GrowthBookHash(1);
    public static GrowthBookHash V2 { get; } = new GrowthBookHash(2);

    private readonly int _version;

    public GrowthBookHash(int version)
    {
        _version = version;
    }

    public double GetAllocationSpot(string salt, string identifier)
    {
        if (_version == 1)
        {
            uint n = FNV32A(identifier + salt);
            return (n % 1000) / 1000.0;
        }
        else if (_version == 2)
        {
            uint n = FNV32A(FNV32A(salt + identifier).ToString());
            return (n % 10000) / 10000;
        }

        return -1; // no allocation match
    }

    /// <summary>
    /// Implementation of the Fowler–Noll–Vo algorithm (fnv32a) algorithm.
    /// https://en.wikipedia.org/wiki/Fowler-Noll-Vo_hash_function
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <returns>The hashed value.</returns>
    static uint FNV32A(string value)
    {
        uint hash = 0x811c9dc5;
        uint prime = 0x01000193;

        foreach (char c in value.AsSpan())
        {
            hash ^= c;
            hash *= prime;
        }

        return hash;
    }
}
