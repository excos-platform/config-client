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
    private IServiceProvider BuildServiceProvider(Action<OptionsBuilder<List<Feature>>> configure, Action<IServiceCollection>? additionalConfig = null)
    {
        var services = new ServiceCollection();
        services.ConfigureExcos<TestOptions>("Test");
        services.AddSingleton<IFeatureProvider, OptionsFeatureProvider>();
        configure(services.AddOptions<List<Feature>>());
        additionalConfig?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public async Task MultipleMatchingVariants_LowestPriorityIsChosen()
    {
        var provider = BuildServiceProvider(o => o.BuildFeature("TestFeature", "Tests")
            .WithFilter(nameof(ContextWithIdentifier.Market)).Matches("US").SaveFilter()
            .Rollout(100, """{"Test": {"Label": "XX"}}""")
            .Configure(f => f.Last().Priority = 2)
            .Rollout(100, """{"Test": {"Label": "YY"}}""")
            .Configure(f => f.Last().Priority = 1)
            .Save());

        var contextual = provider.GetRequiredService<IContextualOptions<TestOptions, ContextWithIdentifier>>();
        var context = new ContextWithIdentifier { Market = "US", AgeGroup = 1 };
        var options = await contextual.GetAsync(context, default);
        var variants = provider.GetRequiredService<IFeatureEvaluation>().EvaluateFeaturesAsync(context, default).ToEnumerable().ToList();

        var metadata = Assert.Single(variants);
        Assert.Equal("TestFeature:Rollout_1", metadata.Id);
    }

    [Fact]
    public async Task ExtensionsBuilder_SetsUpFeatureEasily()
    {
        var provider = BuildServiceProvider(o => o.BuildFeature("TestFeature")
            .WithFilter(nameof(ContextWithIdentifier.Market)).Matches("US").Or().Matches("UK").SaveFilter()
            .Rollout(75, """{"Test": {"Label": "XX"}}""")
            .Save()
            .BuildFeature("TestExperiment")
            .WithFilter(nameof(ContextWithIdentifier.AgeGroup)).InRange(new Range<int>(0, 5, RangeType.IncludeBoth)).SaveFilter()
            .ABExperiment("""{"Test": {"Length": 5}}""", """{"Test": {"Length": 10}}""", nameof(ContextWithIdentifier.SessionId))
            .Save());

        var contextual = provider.GetRequiredService<IContextualOptions<TestOptions, ContextWithIdentifier>>();
        var context = new ContextWithIdentifier { Market = "US", AgeGroup = 1, UserId = "test", SessionId = "testSession" };
        var options = await contextual.GetAsync(context, default);
        var variants = provider.GetRequiredService<IFeatureEvaluation>().EvaluateFeaturesAsync(context, default).ToEnumerable().ToList();

        Assert.Equal(2, variants.Count);
        Assert.Equal("TestFeature:Rollout_0", variants.ElementAt(0).Id);
        Assert.Equal("TestExperiment:B_1", variants.ElementAt(1).Id);

        Assert.Equal("XX", options.Label);
        Assert.Equal(10, options.Length);
    }

    [Fact]
    public async Task DistinctFilters_OneOptionIsChosen()
    {
        var provider = BuildServiceProvider(o => o.BuildFeature("Test1")
            .WithFilter(nameof(ContextWithIdentifier.Market)).Matches("US").Or().Matches("UK").SaveFilter()
            .Rollout(100, """{"Test": {"Label": "X1"}}""")
            .Save()
            .BuildFeature("Test2")
            .WithFilter(nameof(ContextWithIdentifier.Market)).Matches("EU").SaveFilter()
            .Rollout(75, """{"Test": {"Label": "X2"}}""")
            .Save());

        var contextual = provider.GetRequiredService<IContextualOptions<TestOptions, ContextWithIdentifier>>();

        var options = await contextual.GetAsync(new ContextWithIdentifier { Market = "US" }, default);
        Assert.Equal("X1", options.Label);

        options = await contextual.GetAsync(new ContextWithIdentifier { Market = "UK" }, default);
        Assert.Equal("X1", options.Label);

        options = await contextual.GetAsync(new ContextWithIdentifier { Market = "EU" }, default);
        Assert.Equal("X2", options.Label);
    }

    private class TestOptions
    {
        public int Length { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
