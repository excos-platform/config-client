// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.GrowthBook.Models;

public class VariationMeta
{
    /// <summary>
    /// Used to implement holdout groups
    /// </summary>
    public bool? Passthrough { get; set; }

    /// <summary>
    /// A unique key for this variation
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// A human-readable name for this variation
    /// </summary>
    public string? Name { get; set; }
}
