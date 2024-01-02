// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text.Json;

namespace Excos.Options.GrowthBook.Models;

/// <summary>
/// Overrides the defaultValue of a Feature.
/// </summary>
public class FeatureRule
{
    /// <summary>
    /// Optional targeting condition.
    /// </summary>
    public JsonElement Condition { get; set; }

    /// <summary>
    /// What percent of users should be included in the experiment (between 0 and 1, inclusive).
    /// </summary>
    public double Coverage { get; set; } = 1;

    /// <summary>
    /// More precise version of coverage.
    /// </summary>
    public IList<double>? Range { get; set; }

    /// <summary>
    /// Immediately force a specific value (ignore every other option besides condition and coverage).
    /// </summary>
    public JsonElement Force { get; set; }

    /// <summary>
    /// Run an experiment (A/B test) and randomly choose between these variations.
    /// Array of values.
    /// </summary>
    public JsonElement Variations { get; set; }

    /// <summary>
    /// Bucket ranges for variations - so precomputed buckets corresponding to Weights.
    /// </summary>
    public IList<IList<double>>? Ranges { get; set; }

    /// <summary>
    /// The globally unique tracking key for the experiment (default to the feature key).
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// How to weight traffic between variations. Must add to 1.
    /// </summary>
    public IList<double>? Weights { get; set; }

    /// <summary>
    /// Adds the experiment to a namespace.
    /// </summary>
    public JsonElement Namespace { get; set; }

    /// <summary>
    /// What user attribute should be used to assign variations (defaults to id).
    /// </summary>
    public string HashAttribute { get; set; } = "id";

    /// <summary>
    /// Meta info about the experiment variations.
    /// </summary>
    public IList<VariationMeta>? Meta { get; set; }

    public string? Seed { get; set; }

    public string? Phase { get; set; }
}
