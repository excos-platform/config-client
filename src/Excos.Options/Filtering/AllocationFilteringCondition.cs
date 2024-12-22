// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;

namespace Excos.Options.Filtering;

internal class AllocationFilteringCondition : PropertyFilteringCondition
{
    private readonly string _salt;
    private readonly IAllocationHash _allocationHash;
    private readonly Allocation _allocation;

    public AllocationFilteringCondition(string allocationUnit, string salt, IAllocationHash allocationHash, Allocation allocation) : base(allocationUnit)
    {
        _salt = salt;
        _allocationHash = allocationHash;
        _allocation = allocation;
    }

    protected override bool PropertyPredicate<T>(T value)
    {
        var input = value?.ToString() ?? string.Empty;
        var spot = _allocationHash.GetAllocationSpot(_salt, input);
        return _allocation.Contains(spot);
    }
}
