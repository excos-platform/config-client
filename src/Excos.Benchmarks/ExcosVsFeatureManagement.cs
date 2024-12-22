// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text;
using BenchmarkDotNet.Attributes;
using Excos.Options;
using Excos.Options.Contextual;
using Excos.Options.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options.Contextual;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

[MemoryDiagnoser]
public class ExcosVsFeatureManagement
{
    private readonly IServiceProvider _excosProvider;
    private readonly IServiceProvider _fmProvider;

    public ExcosVsFeatureManagement()
    {
        _excosProvider = BuildExcosProvider();
        _fmProvider = BuildFMProvider();
    }

    private IServiceProvider BuildExcosProvider()
    {
        var services = new ServiceCollection();
        services.ConfigureExcos<TestOptions>("Test");
        services.AddExcosOptionsFeatureProvider();
        services.BuildFeature("TestFeature", "Tests")
            .Rollout<TestOptions>(100, (options, name) =>
            {
                options.Setting = "Test";
            })
            .Save();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = false, ValidateOnBuild = false });
    }

    private IServiceProvider BuildFMProvider()
    {
        const string appsettings =
        """
        {
            "FeatureManagement": {
                "TestFeature": {
                    "EnabledFor": [
                        {
                            "Name": "Microsoft.Targeting",
                            "Parameters": {
                                "Audience": {
                                    "DefaultRolloutPercentage": 100,
                                }
                            }
                        }
                    ]
                }
            }
        }
        """;
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appsettings)))
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddFeatureManagement();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = false, ValidateOnBuild = false });
    }

    [Benchmark]
    public object BuildAndResolveExcos()
    {
        var provider = BuildExcosProvider();
        return provider.GetRequiredService<IContextualOptions<TestOptions, TestContext>>();
    }

    [Benchmark]
    public object BuildAndResolveFM()
    {
        var provider = BuildFMProvider();
        return _fmProvider.GetRequiredService<IFeatureManager>();
    }

    [Benchmark]
    public async Task<string> GetExcosSettingsContextual()
    {
        var contextualOptions = _excosProvider.GetRequiredService<IContextualOptions<TestOptions, TestContext>>();
        var options = await contextualOptions.GetAsync(new TestContext(), default);
        return options.Setting;
    }

    [Benchmark]
    public async Task<string> GetExcosSettingsFeatureEvaluation()
    {
        var eval = _excosProvider.GetRequiredService<IFeatureEvaluation>();
        var options = await eval.EvaluateFeaturesAsync<TestOptions, TestContext>(string.Empty, new TestContext(), default);
        return options.Setting;
    }

    [Benchmark]
    public async Task<string> GetFMSetting()
    {
        var featureManagement = _fmProvider.GetRequiredService<IFeatureManager>();
        var options = new TestOptions
        {
            Setting = await featureManagement.IsEnabledAsync("TestFeature", new TestContext()) ? "Test" : string.Empty,
        };
        return options.Setting;
    }

    private class TestOptions
    {
        public string Setting { get; set; } = string.Empty;
    }
}

[OptionsContext]
internal partial class TestContext : ITargetingContext
{
    public string? UserId { get; set; }
    public IEnumerable<string> Groups { get; set; } = Array.Empty<string>();
}
