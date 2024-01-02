// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.ComponentModel.DataAnnotations;

namespace Excos.Options.GrowthBook;

public class GrowthBookOptions
{
    public Uri ApiHost { get; set; } = new Uri("https://cdn.growthbook.io");

    [Required]
    public string ClientKey { get; set; } = string.Empty;

    //public string? DecryptionKey { get; set; } // Only available in paid version

    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    public bool RequestFeaturesOnInitialization { get; set; } = true;
}
