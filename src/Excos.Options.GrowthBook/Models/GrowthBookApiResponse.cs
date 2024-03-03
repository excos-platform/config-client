// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.GrowthBook.Models;

public class GrowthBookApiResponse
{
    public required IDictionary<string, Feature> Features { get; set; }

    public required DateTimeOffset DateUpdated { get; set; }
}
