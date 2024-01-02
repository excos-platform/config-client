// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;

namespace Excos.Options.GrowthBook.Models;

/// <summary>
/// Represents an object consisting of a default value plus rules that can override the default.
/// </summary>
public class Feature
{
    public JsonElement DefaultValue { get; set; }

    public IList<FeatureRule> Rules { get; set; } = new List<FeatureRule>();
}
