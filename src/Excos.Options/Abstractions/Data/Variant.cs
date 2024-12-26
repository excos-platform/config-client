// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// A named variant of a feature.
/// </summary>
public class Variant
{
    /// <summary>
    /// Unique identifier of the variant.
    /// Mainly used in experiment analysis.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Collection of filters for this variant.
    /// When all filters are satisfied, the variant is considered a match.
    /// </summary>
    public IEnumerable<IFilteringCondition> Filters { get; set; } = Enumerable.Empty<IFilteringCondition>();

    /// <summary>
    /// Options object configuration function.
    /// Various feature providers may choose different implementations. 
    /// </summary>
    public required IConfigureOptions Configuration { get; set; }

    /// <summary>
    /// An optional priority.
    /// If more than one variant is matched by filters and allocation,
    /// the priority (lowest first) is used to determine which variant to pick.
    /// </summary>
    public int Priority { get; set; }
}
