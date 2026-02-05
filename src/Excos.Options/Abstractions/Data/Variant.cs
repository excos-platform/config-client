// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;

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
    /// Configuration data for this variant as a JSON structure.
    /// The JSON should follow the configuration hierarchy expected by the consuming options type.
    /// </summary>
    public JsonElement Configuration { get; set; }

    /// <summary>
    /// An optional priority value for variant selection.
    /// When multiple variants match filters and allocation, the priority determines which variant is selected.
    /// Lower numeric values have higher priority (are evaluated first and selected over higher values).
    /// A null priority is treated as the lowest priority (variant is evaluated last).
    /// </summary>
    public int? Priority { get; set; }
}
