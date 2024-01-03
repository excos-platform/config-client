// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.IO.Hashing;
using System.Runtime.InteropServices;
using Excos.Options.Abstractions;

namespace Excos.Options.Utils
{
    internal class XxHashAllocation : IAllocationHash
    {
        public static XxHashAllocation Instance { get; } = new();

        public double GetAllocationSpot(string salt, string identifier)
        {
            var source = $"{salt}_{identifier}";
            var hash = XxHash32.HashToUInt32(MemoryMarshal.AsBytes(source.AsSpan()));
            return (double)hash / uint.MaxValue;
        }
    }
}
