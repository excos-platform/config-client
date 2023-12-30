// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text;
using BenchmarkDotNet.Attributes;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Contextual;
using Excos.Options.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options.Contextual;
using Microsoft.FeatureManagement;

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
        services.AddSingleton<IFeatureProvider, OptionsFeatureProvider>();
        services.AddOptions<FeatureCollection>()
        .Configure(features => features.Add(new Feature
        {
            Name = "TestFeature",
            ProviderName = "Tests",
            Variants =
            {
                new Variant
                {
                    Allocation = Allocation.Percentage(100),
                    Configuration = new BasicConfigureOptions(),
                    Id = "Basic"
                }
            }
        }));

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
                            "Name": "AlwaysOn"
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
        return provider.GetRequiredService<IContextualOptions<TestOptions>>();
    }

    [Benchmark]
    public object BuildAndResolveFM()
    {
        var provider = BuildFMProvider();
        return _fmProvider.GetRequiredService<IFeatureManager>();
    }

    [Benchmark]
    public async Task<string> GetExcosSettings()
    {
        var contextualOptions = _excosProvider.GetRequiredService<IContextualOptions<TestOptions>>();
        var options = await contextualOptions.GetAsync(new TestContext(), default);
        return options.Setting;
    }

    [Benchmark]
    public async Task<bool> GetFMSetting()
    {
        var featureManagement = _fmProvider.GetRequiredService<IFeatureManager>();
        return await featureManagement.IsEnabledAsync("TestFeature");
    }

    private class TestOptions
    {
        public string Setting { get; set; } = string.Empty;
    }

    private class BasicConfigureOptions : IConfigureOptions
    {
        public void Configure<TOptions>(TOptions input, string section) where TOptions : class
        {
            if (input is TestOptions test)
            {
                test.Setting = "Test";
            }
        }
    }
}

[OptionsContext]
internal partial class TestContext
{
    public string? UserId { get; set; }
}
