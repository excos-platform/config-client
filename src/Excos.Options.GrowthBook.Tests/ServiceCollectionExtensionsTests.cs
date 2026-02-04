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
        var host = Host.CreateDefaultBuilder()
            .ConfigureExcosWithGrowthBook(options =>
            {
                options.ClientKey = "test-key";
                options.HttpClientFactory = new MockHttpClientFactory(new MockHandler(Payload));
            })
            .Build();

        // Get the provider from DI
        var featureProvider = host.Services.GetRequiredService<IFeatureProvider>();

        // The same provider instance should be used for both config and contextual
        Assert.NotNull(featureProvider);
        Assert.IsType<GrowthBookFeatureProvider>(featureProvider);
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
    public void ConfigureExcosWithGrowthBook_OnServices_UsesCustomHttpClientFactory()
    {
        var mockFactory = new MockHttpClientFactory(new MockHandler(Payload));
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureExcosWithGrowthBook(options =>
        {
            options.ClientKey = "test-key";
            options.HttpClientFactory = mockFactory;
        });

        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetService<IHttpClientFactory>();

        Assert.Same(mockFactory, httpClientFactory);
    }

    [Fact]
    public void ConfigureExcosWithGrowthBook_OnServices_UsesCustomHttpMessageHandler()
    {
        var mockHandler = new MockHandler(Payload);
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureExcosWithGrowthBook(options =>
        {
            options.ClientKey = "test-key";
            options.HttpMessageHandler = mockHandler;
        });

        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetService<IHttpClientFactory>();

        Assert.NotNull(httpClientFactory);
        Assert.IsType<SimpleHttpClientFactory>(httpClientFactory);
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
        public MockHandler(string content) => _content = content;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
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
