// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions.Data;

public class Variant
{
    public required string Id { get; set; }
    public required Allocation Allocation { get; set; }
    public FilterCollection Filters { get; } = new();
    public required IConfigureOptions Configuration { get; set; }
    public int? Priority { get; set; }
}
