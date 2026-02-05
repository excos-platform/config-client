// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Excos.Options.Contextual;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options.Contextual;
using Xunit;

namespace Excos.Options.GrowthBook.Tests;

public class ServiceCollectionExtensionsTests
{
    private const string Payload =
    """
    {
        "status": 200,
        "features": {
            "my-feature": {
                "defaultValue": {
                    "TestSection": {
                        "Value": "from-growthbook",
                        "Number": 42
                    }
                },
                "rules": []
            }
        },
        "dateUpdated": "2024-01-02T21:22:10.743Z"
    }
    """;

    #region IHostBuilder Extension Tests

    [Fact]
    public void ConfigureExcosWithGrowthBook_OnHostBuilder_RegistersFeatureProvider()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureExcosWithGrowthBook(options =>
            {
                options.ClientKey = "test-key";
                options.HttpClientFactory = new MockHttpClientFactory(new MockHandler(Payload));
            })
            .Build();

        var provider = host.Services.GetService<IFeatureProvider>();
        Assert.NotNull(provider);
    }

    [Fact]
    public void ConfigureExcosWithGrowthBook_OnHostBuilder_ConfigurationContainsGrowthBookValues()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureExcosWithGrowthBook(options =>
            {
                options.ClientKey = "test-key";
                options.HttpClientFactory = new MockHttpClientFactory(new MockHandler(Payload));
            })
            .Build();

        var config = host.Services.GetRequiredService<IConfiguration>();

        // Default values from GrowthBook should be available in configuration
        Assert.Equal("from-growthbook", config.GetValue<string>("TestSection:Value"));
        Assert.Equal(42, config.GetValue<int>("TestSection:Number"));
    }

    [Fact]
    public async Task ConfigureExcosWithGrowthBook_OnHostBuilder_ContextualOptionsWork()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureExcosWithGrowthBook(options =>
            {
                options.ClientKey = "test-key";
                options.HttpClientFactory = new MockHttpClientFactory(new MockHandler(Payload));
            })
            .ConfigureServices((_, services) =>
            {
                services.ConfigureExcos<ExtensionTestOptions>("TestSection");
            })
            .Build();

        var contextualOptions = host.Services.GetRequiredService<IContextualOptions<ExtensionTestOptions, ExtensionTestContext>>();
        var context = new ExtensionTestContext { UserId = "user-123" };

        var options = await contextualOptions.GetAsync(context, default);

        Assert.Equal("from-growthbook", options.Value);
        Assert.Equal(42, options.Number);
    }

    [Fact]
    public void ConfigureExcosWithGrowthBook_OnHostBuilder_SharedProviderBetweenConfigAndContextual()
    {
        // When using the integrated HostBuilder extension, there should be only one HTTP call
        // because the same provider instance is shared between config and contextual options
        var mockHandler = new MockHandler(Payload);
        var host = Host.CreateDefaultBuilder()
            .ConfigureExcosWithGrowthBook(options =>
            {
                options.ClientKey = "test-key";
                options.RequestFeaturesOnInitialization = true;
                options.HttpClientFactory = new MockHttpClientFactory(mockHandler);
            })
            .Build();

        // Trigger initialization by resolving services
        _ = host.Services.GetRequiredService<IFeatureProvider>();
        _ = host.Services.GetRequiredService<IConfiguration>();

        // Should only have made one HTTP call since provider is shared
        Assert.Equal(1, mockHandler.CallCount);
    }

    [Fact]
    public void ConfigureExcosWithGrowthBook_SeparateExtensions_SeparateProviders()
    {
        // When using separate config and service collection extensions, there should be two HTTP calls
        // because each creates its own provider instance
        var mockHandler = new MockHandler(Payload);
        var mockFactory = new MockHttpClientFactory(mockHandler);

        // First: configuration extension
        var config = new ConfigurationBuilder()
            .AddExcosGrowthBookConfiguration(options =>
            {
                options.ClientKey = "test-key";
                options.RequestFeaturesOnInitialization = true;
                options.HttpClientFactory = mockFactory;
            })
            .Build();

        // Second: service collection extension
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureExcosWithGrowthBook(options =>
        {
            options.ClientKey = "test-key";
            options.RequestFeaturesOnInitialization = true;
            options.HttpClientFactory = mockFactory;
        });
        var provider = services.BuildServiceProvider();

        // Trigger initialization
        _ = config["TestSection:Value"];
        _ = provider.GetRequiredService<IFeatureProvider>();

        // Should have made two HTTP calls since providers are separate
        Assert.Equal(2, mockHandler.CallCount);
    }

    #endregion

    #region IServiceCollection Extension Tests

    [Fact]
    public void ConfigureExcosWithGrowthBook_OnServices_RegistersFeatureProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureExcosWithGrowthBook(options =>
        {
            options.ClientKey = "test-key";
            options.HttpClientFactory = new MockHttpClientFactory(new MockHandler(Payload));
        });

        var provider = services.BuildServiceProvider();
        var featureProvider = provider.GetService<IFeatureProvider>();

        Assert.NotNull(featureProvider);
    }

    [Fact]
    public async Task ConfigureExcosWithGrowthBook_OnServices_UsesCustomHttpClientFactory()
    {
        // Custom HttpClientFactory should be used by GrowthBook but NOT registered in DI
        var mockHandler = new MockHandler(Payload);
        var mockFactory = new MockHttpClientFactory(mockHandler);
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureExcosWithGrowthBook(options =>
        {
            options.ClientKey = "test-key";
            options.HttpClientFactory = mockFactory;
        });

        var provider = services.BuildServiceProvider();

        // Should NOT be registered in DI (doesn't pollute global container)
        var httpClientFactory = provider.GetService<IHttpClientFactory>();
        Assert.Null(httpClientFactory);

        // But GrowthBook should still work using the custom factory
        var featureProvider = provider.GetRequiredService<IFeatureProvider>();
        var features = await featureProvider.GetFeaturesAsync(default);
        Assert.Single(features);
        Assert.True(mockHandler.CallCount > 0);
    }

    [Fact]
    public async Task ConfigureExcosWithGrowthBook_OnServices_UsesCustomHttpMessageHandler()
    {
        // Custom HttpMessageHandler should be used by GrowthBook but NOT registered in DI
        var mockHandler = new MockHandler(Payload);
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureExcosWithGrowthBook(options =>
        {
            options.ClientKey = "test-key";
            options.HttpMessageHandler = mockHandler;
        });

        var provider = services.BuildServiceProvider();

        // Should NOT be registered in DI (doesn't pollute global container)
        var httpClientFactory = provider.GetService<IHttpClientFactory>();
        Assert.Null(httpClientFactory);

        // But GrowthBook should still work using the custom handler
        var featureProvider = provider.GetRequiredService<IFeatureProvider>();
        var features = await featureProvider.GetFeaturesAsync(default);
        Assert.Single(features);
        Assert.True(mockHandler.CallCount > 0);
    }

    [Fact]
    public async Task ConfigureExcosWithGrowthBook_OnServices_CanFetchFeatures()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureExcosWithGrowthBook(options =>
        {
            options.ClientKey = "test-key";
            options.HttpClientFactory = new MockHttpClientFactory(new MockHandler(Payload));
        });

        var provider = services.BuildServiceProvider();
        var featureProvider = provider.GetRequiredService<IFeatureProvider>();

        var features = await featureProvider.GetFeaturesAsync(default);

        Assert.Single(features);
        Assert.Equal("my-feature", features.First().Name);
    }

    [Fact]
    public async Task ConfigureExcosWithGrowthBook_OnServices_ContextualOptionsWork()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureExcosWithGrowthBook(options =>
        {
            options.ClientKey = "test-key";
            options.HttpClientFactory = new MockHttpClientFactory(new MockHandler(Payload));
        });
        services.ConfigureExcos<ExtensionTestOptions>("TestSection");

        var provider = services.BuildServiceProvider();
        var contextualOptions = provider.GetRequiredService<IContextualOptions<ExtensionTestOptions, ExtensionTestContext>>();
        var context = new ExtensionTestContext { UserId = "user-123" };

        var options = await contextualOptions.GetAsync(context, default);

        Assert.Equal("from-growthbook", options.Value);
        Assert.Equal(42, options.Number);
    }

    #endregion

    #region Test Helpers

    private class MockHttpClientFactory : IHttpClientFactory
    {
        private readonly DelegatingHandler _handler;
        public MockHttpClientFactory(DelegatingHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler);
        }
    }

    private class MockHandler : DelegatingHandler
    {
        private readonly string _content;
        public int CallCount { get; private set; }
        public MockHandler(string content) => _content = content;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage();
            response.Content = new StringContent(_content);
            return Task.FromResult(response);
        }
    }

    #endregion
}

// Options and context must be at namespace level for source generation
public class ExtensionTestOptions
{
    public string Value { get; set; } = "default";
    public int Number { get; set; } = 0;
}

[OptionsContext]
public partial struct ExtensionTestContext
{
    public string UserId { get; set; }
}
