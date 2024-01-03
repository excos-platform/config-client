// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Utils;

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// A named variant of a feature.
/// </summary>
public class Variant
{
    /// <summary>
    /// Identifier of the variant.
    /// Mainly used in experiment analysis.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Allocation of the variant a range of users who otherwise satisfy the filters.
    /// </summary>
    public required Allocation Allocation { get; set; }

    /// <summary>
    /// Collection of filters for this variant.
    /// </summary>
    public FilterCollection Filters { get; } = new();

    /// <summary>
    /// Options object configuration function.
    /// Various feature providers may choose different implementations. 
    /// </summary>
    public required IConfigureOptions Configuration { get; set; }

    /// <summary>
    /// An optional priority.
    /// If more than one variant is matched by filters and allocation,
    /// the priority (lowest first) is used to determine which variant to pick.
    /// If priority is not specified, the first variant with the highest number of filtered properties will be chosen.
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Name of the property in context which should be used for allocation calculation for this variant.
    /// Overridable for compatibility with external providers. Ideally you should use the same allocation unit for all variants on the feature level.
    /// </summary>
    public string? AllocationUnit { get; set; }

    /// <summary>
    /// Hashing algorithm used for variant allocation calculations.
    /// </summary>
    public IAllocationHash AllocationHash { get; set; } = XxHashAllocation.Instance;
}
