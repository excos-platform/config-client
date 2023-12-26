// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Contextual;
using Excos.Options.Providers;
using Excos.Options.Tests.Contextual;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Options.Contextual;
using Xunit;

namespace Excos.Options.Tests;

public class OptionsBasedFeaturesTests
{
    private IServiceProvider BuildServiceProvider(Action<OptionsBuilder<FeatureCollection>> configure)
    {
        var services = new ServiceCollection();
        services.ConfigureExcos<TestOptions>("Test");
        services.AddSingleton<IFeatureProvider, OptionsFeatureProvider>();
        configure(services.AddOptions<FeatureCollection>());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public async Task FeatureMetadataIsPopulated()
    {
        var provider = BuildServiceProvider(o => o.Configure(features => features.Add(new Feature
        {
            Name = "TestFeature",
            ProviderName = "Tests",
            Filters =
            {
                new Filter
                {
                    PropertyName = "UserId",
                    Conditions =
                    {
                        new StringFilteringCondition("user1"),
                    },
                },
            },
            Variants =
            {
                new Variant
                {
                    Allocation = new Allocation(new Range<double>(0, 1, RangeType.IncludeBoth)),
                    Configuration = new NullConfigureOptions(),
                    Id = "EmptyVariant"
                }
            }
        })));

        var contextual = provider.GetRequiredService<IContextualOptions<TestOptions>>();
        var optionsWithUser = await contextual.GetAsync(new ContextWithIdentifier { UserId = "user1" }, default);
        var optionsWithoutUser = await contextual.GetAsync(new ContextWithIdentifier(), default);

        Assert.Null(optionsWithoutUser.Metadata);

        Assert.NotNull(optionsWithUser.Metadata);
        var metadata = Assert.Single(optionsWithUser.Metadata.Features);
        Assert.Equal("TestFeature", metadata.FeatureName);
        Assert.Equal("Tests", metadata.FeatureProvider);
        Assert.Equal("EmptyVariant", metadata.VariantId);
    }

    private class TestOptions
    {
        public int Length { get; set; }
        public string Label { get; set; } = string.Empty;
        public FeatureMetadata? Metadata { get; set; }
    }
}
