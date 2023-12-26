// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions.Data;

/// <summary>
/// Metadata of a variant override.
/// </summary>
public class VariantOverride
{
    /// <summary>
    /// Id of the variant which is to be chosen.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Name of the override provider.
    /// </summary>
    public required string OverrideProviderName { get; set; }
}
