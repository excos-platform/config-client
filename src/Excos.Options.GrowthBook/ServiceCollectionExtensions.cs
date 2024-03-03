// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Excos.Options.GrowthBook
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureExcosWithGrowthBook(this IServiceCollection services)
        {
            services.AddOptions<GrowthBookOptions>()
                .Validate(options => !string.IsNullOrEmpty(options.ClientKey));

            // Filter out the HttpClient logs for this library
            services.AddLogging(builder => builder.AddFilter((_, category, _) => category?.StartsWith($"System.Net.Http.HttpClient.{nameof(GrowthBook)}") != true));

            services.AddHttpClient(nameof(GrowthBook));
            services.AddSingleton<GrowthBookApiCaller>();
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IFeatureProvider), typeof(GrowthBookFeatureProvider), ServiceLifetime.Singleton));

            return services;
        }
    }
}
