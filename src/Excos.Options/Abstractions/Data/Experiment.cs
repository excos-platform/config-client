// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions.Data;

public class Experiment
{
    public required string Name { get; set; }
    public required string ProviderName { get; set; }
    public bool Enabled { get; set; } = true;
    public FilterCollection Filters { get; } = new();
    public VariantCollection Variants { get; } = new();
    public string Salt
    {
        get
        {
            if (_salt == null)
            {
                _salt = Name;
            }

            return _salt;
        }
        set => _salt = value;
    }

    private string? _salt = null;
}
