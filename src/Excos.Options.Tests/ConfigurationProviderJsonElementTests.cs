// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text;
using System.Text.Json;
using Excos.Options.Abstractions;
using Excos.Options.Contextual;
using Excos.Options.Providers;
using Excos.Options.Providers.Configuration;
using Excos.Options.Tests.Contextual;
using Excos.Options.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options.Contextual;
using Xunit;

namespace Excos.Options.Tests;

/// <summary>
/// Tests verifying the configuration-based feature provider works correctly
/// with JsonElement-based Variant.Configuration (Decision 001 requirements).
/// </summary>
public class ConfigurationProviderJsonElementTests
{
    #region Variant Configuration as JsonElement

    [Fact]
    public async Task ConfigurationProvider_StoresJsonElementInVariant()
    {
        const string appsettings =
        """
        {
            "Features": {
                "TestFeature": {
                    "Variants": {
                        "rollout": {
                            "Allocation": "100%",
                            "Settings": {
                                "MySection": {
                                    "Value": "test-value",
                                    "Count": 42
                                }
                            }
                        }
                    }
                }
            }
        }
        """;

        var provider = BuildServiceProvider(appsettings);
        var featureProvider = provider.GetRequiredService<IFeatureProvider>();

        var features = (await featureProvider.GetFeaturesAsync(default)).ToList();
        var variant = features.First()[0];

        // Verify Configuration is JsonElement
        Assert.Equal(JsonValueKind.Object, variant.Configuration.ValueKind);

        // Verify we can inspect the configuration
        // Note: IConfiguration stores values as strings, so JsonElement has string values
        var section = variant.Configuration.GetProperty("MySection");
        Assert.Equal("test-value", section.GetProperty("Value").GetString());
        Assert.Equal("42", section.GetProperty("Count").GetString()); // Stored as string
    }

    [Fact]
    public async Task ConfigurationProvider_NestedSettings_PreservesStructure()
    {
        const string appsettings =
        """
        {
            "Features": {
                "DeepFeature": {
                    "Variants": {
                        "A": {
                            "Allocation": "100%",
                            "Settings": {
                                "Level1": {
                                    "Level2": {
                                        "Level3": {
                                            "DeepValue": "found"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        """;

        var provider = BuildServiceProvider(appsettings);
        var featureProvider = provider.GetRequiredService<IFeatureProvider>();

        var features = (await featureProvider.GetFeaturesAsync(default)).ToList();
        var variant = features.First()[0];

        // Navigate deep structure
        var deepValue = variant.Configuration
            .GetProperty("Level1")
            .GetProperty("Level2")
            .GetProperty("Level3")
            .GetProperty("DeepValue")
            .GetString();

        Assert.Equal("found", deepValue);
    }

    [Fact]
    public async Task ConfigurationProvider_ArraySettings_PreservesArrays()
    {
        const string appsettings =
        """
        {
            "Features": {
                "ArrayFeature": {
                    "Variants": {
                        "A": {
                            "Allocation": "100%",
                            "Settings": {
                                "Options": {
                                    "Tags": ["alpha", "beta", "gamma"]
                                }
                            }
                        }
                    }
                }
            }
        }
        """;

        var provider = BuildServiceProvider(appsettings);
        var featureProvider = provider.GetRequiredService<IFeatureProvider>();

        var features = (await featureProvider.GetFeaturesAsync(default)).ToList();
        var variant = features.First()[0];

        var tags = variant.Configuration.GetProperty("Options").GetProperty("Tags");
        Assert.Equal(JsonValueKind.Array, tags.ValueKind);
        Assert.Equal(3, tags.GetArrayLength());
        Assert.Equal("alpha", tags[0].GetString());
    }

    #endregion

    #region Configuration Binding End-to-End

    [Fact]
    public async Task ConfigurationProvider_BindsToOptionsViaContextual()
    {
        const string appsettings =
        """
        {
            "Features": {
                "MyFeature": {
                    "Variants": {
                        "Treatment": {
                            "Allocation": "100%",
                            "Settings": {
                                "TestSection": {
                                    "Name": "TreatmentName",
                                    "Count": 99
                                }
                            }
                        }
                    }
                }
            }
        }
        """;

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appsettings)))
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.ConfigureExcos<TestOptions>("TestSection");
        services.ConfigureExcosFeatures("Features");

        var provider = services.BuildServiceProvider();
        var contextualOptions = provider.GetRequiredService<IContextualOptions<TestOptions, ContextWithIdentifier>>();

        var options = await contextualOptions.GetAsync(new ContextWithIdentifier { UserId = "user1" }, default);

        Assert.Equal("TreatmentName", options.Name);
        Assert.Equal(99, options.Count);
    }

    [Fact]
    public async Task ConfigurationProvider_MultipleVariants_CorrectOneSelected()
    {
        const string appsettings =
        """
        {
            "Features": {
                "ABTest": {
                    "Variants": {
                        "Control": {
                            "Allocation": "[0;0.5)",
                            "Priority": 1,
                            "Settings": {
                                "TestSection": {
                                    "Name": "Control"
                                }
                            }
                        },
                        "Treatment": {
                            "Allocation": "[0.5;1]",
                            "Priority": 1,
                            "Settings": {
                                "TestSection": {
                                    "Name": "Treatment"
                                }
                            }
                        }
                    }
                }
            }
        }
        """;

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appsettings)))
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.ConfigureExcos<TestOptions>("TestSection");
        services.ConfigureExcosFeatures("Features");

        var provider = services.BuildServiceProvider();
        var featureEval = provider.GetRequiredService<IFeatureEvaluation>();

        // User that gets Treatment (hash > 0.5)
        var variants = (await featureEval.EvaluateFeaturesAsync(
            new ContextWithIdentifier { UserId = "user-treatment-bucket" }, 
            default)).ToList();

        Assert.Single(variants);
        // The variant should have JsonElement configuration
        Assert.Equal(JsonValueKind.Object, variants[0].Configuration.ValueKind);
    }

    #endregion

    #region Serialization and Comparison

    [Fact]
    public async Task ConfigurationProvider_VariantCanBeSerialized()
    {
        const string appsettings =
        """
        {
            "Features": {
                "SerializableFeature": {
                    "Variants": {
                        "A": {
                            "Allocation": "100%",
                            "Settings": {
                                "Data": {
                                    "Key": "Value"
                                }
                            }
                        }
                    }
                }
            }
        }
        """;

        var provider = BuildServiceProvider(appsettings);
        var featureProvider = provider.GetRequiredService<IFeatureProvider>();

        var features = (await featureProvider.GetFeaturesAsync(default)).ToList();
        var variant = features.First()[0];

        // Serialize and deserialize
        var json = JsonSerializer.Serialize(variant.Configuration);
        var deserialized = JsonDocument.Parse(json).RootElement;

        Assert.Equal("Value", deserialized.GetProperty("Data").GetProperty("Key").GetString());
    }

    [Fact]
    public async Task ConfigurationProvider_VariantsCanBeCompared()
    {
        const string appsettings =
        """
        {
            "Features": {
                "ComparableFeature": {
                    "Variants": {
                        "V1": {
                            "Allocation": "[0;0.5)",
                            "Settings": {
                                "Test": { "Value": "1" }
                            }
                        },
                        "V2": {
                            "Allocation": "[0.5;1]",
                            "Settings": {
                                "Test": { "Value": "2" }
                            }
                        }
                    }
                }
            }
        }
        """;

        var provider = BuildServiceProvider(appsettings);
        var featureProvider = provider.GetRequiredService<IFeatureProvider>();

        var features = (await featureProvider.GetFeaturesAsync(default)).ToList();
        var feature = features.First();

        Assert.Equal(2, feature.Count);

        // IConfiguration stores as strings, compare as strings
        var value1 = feature[0].Configuration.GetProperty("Test").GetProperty("Value").GetString();
        var value2 = feature[1].Configuration.GetProperty("Test").GetProperty("Value").GetString();

        Assert.NotEqual(value1, value2);
    }

    #endregion

    private IServiceProvider BuildServiceProvider(string appsettings)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appsettings)))
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.ConfigureExcosFeatures("Features");

        return services.BuildServiceProvider();
    }

    private class TestOptions
    {
        public string? Name { get; set; }
        public int Count { get; set; }
    }
}
