// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;

namespace Excos.Options.Filtering;

public class NeverFilteringCondition : IFilteringCondition
{
    public static NeverFilteringCondition Instance { get; } = new();

    public bool IsSatisfiedBy<T>(T value) => false;
}
