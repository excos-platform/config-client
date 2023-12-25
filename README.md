# Excos - .NET feature management and experimentation

This library aims to provide experiment configuration framework on top of [Microsoft.Extensions.Options.Contextual](https://www.nuget.org/packages/Microsoft.Extensions.Options.Contextual) ([source](https://github.com/dotnet/extensions/tree/main/src/Libraries/Microsoft.Extensions.Options.Contextual)) and guide you through the steps to set up experiments in your application.

> [!WARNING]  
> It's very much a Work In Progress. Once it reaches some level of maturity I will publish a NuGet package.

I got inspired to start working on this after writing [a blog post on experimentation](https://devblog.dziubiak.pl/web/02-experimentation/).

## Usage (planned)

Define your context class that will be passed to `IContextualOptions.GetAsync()`.
The first property named `Identifier` or ending with `Id` will be used for the allocation calculations.

```csharp
[OptionsContext]
internal partial class WeatherForecastContext
{
    public Guid UserId { get; set; }
    public string? Country { get; set; }
}
```

Define your options class like you usually would.
You can add the `FeatureMetadata` property to your options model to receive experiment metadata, such as the variant IDs.

```csharp
internal class WeatherForecastOptions
{
    public string TemperatureScale { get; set; } = "Celsius"; // Celsius or Fahrenheit
    public int ForecastDays { get; set; }
    public FeatureMetadata FeatureMetadata { get; set; }
}
```

Bind the options to a configuration section.

```csharp
services.ConfigureExcos<WeatherForecastOptions>("Forecast");
```

Configure Excos experiment via configuration or fluent code.

```
coming soon...
```

Inject `IContextualOptions` into your service.

```csharp
internal class WeatherForecast
{
    public DateTime Date { get; set; }
    public int Temperature { get; set; }
    public string TemperatureScale { get; set; } = string.Empty;
}

internal class WeatherForecastService
{
    private readonly IContextualOptions<WeatherForecastOptions> _contextualOptions;
    private readonly Random _rng = new(0);

    public WeatherForecastService(IContextualOptions<WeatherForecastOptions> contextualOptions)
    {
        _contextualOptions = contextualOptions;
    }

    public async Task<IEnumerable<WeatherForecast>> GetForecast(WeatherForecastContext context, CancellationToken cancellationToken)
    {
        WeatherForecastOptions options = await _contextualOptions.GetAsync(context, cancellationToken);
        return Enumerable.Range(1, options.ForecastDays).Select(index => new WeatherForecast
        {
            Date = new DateTime(2000, 1, 1).AddDays(index),
            Temperature = _rng.Next(-20, 55),
            TemperatureScale = options.TemperatureScale,
        });
    }
}
```

You can also allow overriding the variant based on some context - example:

```csharp
class TestUserOverride : IFeatureVariantOverride
{
    public Task<VariantOverride?> TryOverrideAsync<TContext>(Feature experiment, TContext optionsContext, CancellationToken cancellationToken)
        where TContext : IOptionsContext
    {
        var receiver = new Receiver();
        optionsContext.PopulateReceiver(receiver);
        if (experiment.Name == "MyExp" && receiver.UserId.IsTestUser())
        {
            return new VariantOverride
            {
                Id = "MyVariant",
                OverrideProviderName = nameof(TestUserOverride),
            };
        }

        return null;
    }

    private class Receiver : IOptionsContextReceiver
    {
        public Guid UserId;
        public void Receive<T>(string key, T value)
        {
            if (key == nameof(UserId))
            {
                UserId = (Guid)value;
            }
        }
    }
}
```

You can use the Excos framework to rollout features or as feature gates. In that case your experiment would have only one variant and you can control the rollout allocation similarly to how you would partition your audience for experiments.

## Roadmap

1. Experiment data model implementation and contextual options loader. With options based provider.
2. Extension methods for fluent experiment setup (over options provider).
3. EF Core based provider.
4. GrowthBook integration (configuration provider + experiments provider).
