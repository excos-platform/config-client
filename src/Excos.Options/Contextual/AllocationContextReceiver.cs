// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.IO.Hashing;
using System.Runtime.InteropServices;
using Excos.Options.Utils;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Contextual;

/// <summary>
/// Context receiver for determining allocation spot based on context.
/// </summary>
internal class AllocationContextReceiver : IOptionsContextReceiver, IDisposable
{
    private string _allocationUnit;
    private string _salt;
    private string _value = string.Empty;

    private AllocationContextReceiver(string allocationUnit, string salt)
    {
        _allocationUnit = allocationUnit;
        _salt = salt;
    }

    public void Receive<T>(string key, T value)
    {
        if (string.Equals(key, _allocationUnit, StringComparison.InvariantCultureIgnoreCase))
        {
            _value = value?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Compute an allocation spot (floating point value between 0 and 1) for the identifier from context.
    /// </summary>
    public double GetIdentifierAllocationSpot()
    {
        var source = $"{_salt}_{_value}";
        var hash = XxHash32.HashToUInt32(MemoryMarshal.AsBytes(source.AsSpan()));
        return (double)hash / uint.MaxValue;
    }

    public void Dispose() => Return(this);

    public static AllocationContextReceiver Get(string allocationUnit, string salt)
    {
        if (PrivateObjectPool<AllocationContextReceiver>.Instance.TryGet(out var instance) && instance != null)
        {
            instance._allocationUnit = allocationUnit;
            instance._salt = salt;
            instance._value = string.Empty;
        }
        else
        {
            instance = new AllocationContextReceiver(allocationUnit, salt);
        }

        return instance;
    }

    public static void Return(AllocationContextReceiver instance) =>
        PrivateObjectPool<AllocationContextReceiver>.Instance.Return(instance);
}
