// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options;

/// <summary>
/// Options for the Excos feature management.
/// </summary>
public sealed class ExcosOptions
{
    /// <summary>
    /// The default allocation unit used for features if there is no override in the feature configuration.
    /// </summary>
    /// <remarks>
    /// By default it's set to <c>UserId</c>.
    /// </remarks>
    public string DefaultAllocationUnit { get; set; } = "UserId";
}
