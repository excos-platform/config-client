// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Contextual;
using Excos.Options.Filtering;
using Excos.Options.Providers;
using Excos.Options.Tests.Contextual;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Options.Contextual;
using Xunit;

namespace Excos.Options.Tests;

public class OptionsBasedFeaturesTests
{
    private IServiceProvider BuildServiceProvider(Action<OptionsBuilder<FeatureCollection>> configure, Action<IServiceCollection>? additionalConfig = null)
    {
        var services = new ServiceCollection();
        services.ConfigureExcos<TestOptions>("Test");
        services.AddSingleton<IFeatureProvider, OptionsFeatureProvider>();
        configure(services.AddOptions<FeatureCollection>());
        additionalConfig?.Invoke(services);

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
                    Allocation = Allocation.Percentage(100),
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

    [Fact]
    public async Task MultipleMatchingVariants_MostFiltersIsChosen()
    {
        var provider = BuildServiceProvider(o => o.Configure(features => features.Add(new Feature
        {
            Name = "TestFeature",
            ProviderName = "Tests",
            Variants =
            {
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "NoFilter"
                },
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "Filtered1",
                    Filters =
                    {
                        new Filter
                        {
                            PropertyName = "Market",
                            Conditions = { new StringFilteringCondition("US") }
                        }
                    }
                },
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "Filtered2",
                    Filters =
                    {
                        new Filter
                        {
                            PropertyName = "Market",
                            Conditions = { new StringFilteringCondition("US") }
                        },
                        new Filter
                        {
                            PropertyName = "AgeGroup",
                            Conditions = { new RangeFilteringCondition<int>(new Range<int>(1,2,RangeType.IncludeBoth)) }
                        }
                    }
                }
            }
        })));

        var contextual = provider.GetRequiredService<IContextualOptions<TestOptions>>();
        var options = await contextual.GetAsync(new ContextWithIdentifier { Market = "US", AgeGroup = 1 }, default);

        Assert.NotNull(options.Metadata);
        var metadata = Assert.Single(options.Metadata.Features);
        Assert.Equal("TestFeature", metadata.FeatureName);
        Assert.Equal("Filtered2", metadata.VariantId);
    }

    [Fact]
    public async Task MultipleMatchingVariants_PriorityOverMostFiltersIsChosen()
    {
        var provider = BuildServiceProvider(o => o.Configure(features => features.Add(new Feature
        {
            Name = "TestFeature",
            ProviderName = "Tests",
            Variants =
            {
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "Priority",
                    Priority = 1
                },
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "Filtered1",
                    Filters =
                    {
                        new Filter
                        {
                            PropertyName = "Market",
                            Conditions = { new StringFilteringCondition("US") }
                        }
                    }
                },
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "Filtered2",
                    Filters =
                    {
                        new Filter
                        {
                            PropertyName = "Market",
                            Conditions = { new StringFilteringCondition("US") }
                        },
                        new Filter
                        {
                            PropertyName = "AgeGroup",
                            Conditions = { new RangeFilteringCondition<int>(new Range<int>(1,2,RangeType.IncludeBoth)) }
                        }
                    }
                }
            }
        })));

        var contextual = provider.GetRequiredService<IContextualOptions<TestOptions>>();
        var options = await contextual.GetAsync(new ContextWithIdentifier { Market = "US", AgeGroup = 1 }, default);

        Assert.NotNull(options.Metadata);
        var metadata = Assert.Single(options.Metadata.Features);
        Assert.Equal("TestFeature", metadata.FeatureName);
        Assert.Equal("Priority", metadata.VariantId);
    }

    [Fact]
    public async Task MultipleMatchingVariants_LowestPriorityIsChosen()
    {
        var provider = BuildServiceProvider(o => o.Configure(features => features.Add(new Feature
        {
            Name = "TestFeature",
            ProviderName = "Tests",
            Variants =
            {
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "PriorityOnly",
                    Priority = 2
                },
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "FilteredWithPriority",
                    Priority = 1,
                    Filters =
                    {
                        new Filter
                        {
                            PropertyName = "Market",
                            Conditions = { new StringFilteringCondition("US") }
                        }
                    }
                },
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "Filtered2",
                    Priority = 3,
                    Filters =
                    {
                        new Filter
                        {
                            PropertyName = "Market",
                            Conditions = { new StringFilteringCondition("US") }
                        },
                        new Filter
                        {
                            PropertyName = "AgeGroup",
                            Conditions = { new RangeFilteringCondition<int>(new Range<int>(1,2,RangeType.IncludeBoth)) }
                        }
                    }
                }
            }
        })));

        var contextual = provider.GetRequiredService<IContextualOptions<TestOptions>>();
        var options = await contextual.GetAsync(new ContextWithIdentifier { Market = "US", AgeGroup = 1 }, default);

        Assert.NotNull(options.Metadata);
        var metadata = Assert.Single(options.Metadata.Features);
        Assert.Equal("TestFeature", metadata.FeatureName);
        Assert.Equal("FilteredWithPriority", metadata.VariantId);
    }

    [Fact]
    public async Task MultipleMatchingVariants_WithSamePriorityMostFiltersIsChosen()
    {
        var provider = BuildServiceProvider(o => o.Configure(features => features.Add(new Feature
        {
            Name = "TestFeature",
            ProviderName = "Tests",
            Variants =
            {
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "PriorityOnly",
                    Priority = 2
                },
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "Filtered1",
                    Priority = 1,
                    Filters =
                    {
                        new Filter
                        {
                            PropertyName = "Market",
                            Conditions = { new StringFilteringCondition("US") }
                        }
                    }
                },
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "Filtered2",
                    Priority = 1,
                    Filters =
                    {
                        new Filter
                        {
                            PropertyName = "Market",
                            Conditions = { new StringFilteringCondition("US") }
                        },
                        new Filter
                        {
                            PropertyName = "AgeGroup",
                            Conditions = { new RangeFilteringCondition<int>(new Range<int>(1,2,RangeType.IncludeBoth)) }
                        }
                    }
                }
            }
        })));

        var contextual = provider.GetRequiredService<IContextualOptions<TestOptions>>();
        var options = await contextual.GetAsync(new ContextWithIdentifier { Market = "US", AgeGroup = 1 }, default);

        Assert.NotNull(options.Metadata);
        var metadata = Assert.Single(options.Metadata.Features);
        Assert.Equal("TestFeature", metadata.FeatureName);
        Assert.Equal("Filtered2", metadata.VariantId);
    }

    [Fact]
    public async Task WithOverride_ChoosesOverride()
    {
        var provider = BuildServiceProvider(o => o.Configure(features => features.Add(new Feature
        {
            Name = "TestFeature",
            ProviderName = "Tests",
            Variants =
            {
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "WW",
                },
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new NullConfigureOptions(),
                    Id = "US",
                    Filters =
                    {
                        new Filter
                        {
                            PropertyName = "Market",
                            Conditions = { new StringFilteringCondition("US") }
                        }
                    }
                }
            }
        })), services =>
        {
            services.AddSingleton<IFeatureVariantOverride>(new TestOverride("TestFeature", "US"));
        });

        var contextual = provider.GetRequiredService<IContextualOptions<TestOptions>>();
        var options = await contextual.GetAsync(new ContextWithIdentifier { Market = "PL" }, default);

        Assert.NotNull(options.Metadata);
        var metadata = Assert.Single(options.Metadata.Features);
        Assert.Equal("TestFeature", metadata.FeatureName);
        Assert.Equal("US", metadata.VariantId);
        Assert.True(metadata.IsOverridden);
        Assert.Equal(nameof(TestOverride), metadata.OverrideProviderName);
    }

    private class TestOptions
    {
        public int Length { get; set; }
        public string Label { get; set; } = string.Empty;
        public FeatureMetadata? Metadata { get; set; }
    }

    private class TestOverride : IFeatureVariantOverride
    {
        private readonly string _featureName;
        private readonly string _variantId;

        public TestOverride(string featureName, string variantId)
        {
            _featureName = featureName;
            _variantId = variantId;
        }

        public Task<VariantOverride?> TryOverrideAsync<TContext>(Feature feature, TContext optionsContext, CancellationToken cancellationToken) where TContext : IOptionsContext
        {
            if (feature.Name == _featureName)
            {
                return Task.FromResult<VariantOverride?>(new VariantOverride
                {
                    Id = _variantId,
                    OverrideProviderName = nameof(TestOverride),
                });
            }

            return Task.FromResult<VariantOverride?>(null);
        }
    }
}
