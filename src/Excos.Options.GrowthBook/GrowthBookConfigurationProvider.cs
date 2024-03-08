// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Configuration;

namespace Excos.Options.GrowthBook
{
    internal class GrowthBookConfigurationProvider : ConfigurationProvider
    {
        public void SetData(IDictionary<string, string?> data)
        {
            Data = data;
            OnReload();
        }
    }

    internal class GrowthBookConfigurationSource : IConfigurationSource
    {
        public GrowthBookConfigurationProvider? GrowthBookConfigurationProvider { get; set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            GrowthBookConfigurationProvider = new GrowthBookConfigurationProvider();

            return GrowthBookConfigurationProvider;
        }
    }
}
