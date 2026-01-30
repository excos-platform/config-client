# Usage

## Getting started

Following the Options.Contextual docs:

1\. Define your context class that will be passed to `IContextualOptions.GetAsync()`.
By default `UserId` property is used for allocation calculations. You can override it for a single feature by configuring the `AllocationUnit` property.

```csharp
[OptionsContext]
internal partial class WeatherForecastContext
{
    public Guid UserId { get; set; }
    public string? Country { get; set; }
}
```

2\. Define your options class like you usually would.
You can add a property of type `FeatureMetadata` to your options model to receive experiment metadata, such as the variant IDs.

```csharp
internal class WeatherForecastOptions
{
    public string TemperatureScale { get; set; } = "Celsius"; // Celsius or Fahrenheit
    public int ForecastDays { get; set; }
    public FeatureMetadata? Metadata { get; set; }
}
```

3\. Bind the options to a configuration section.

```csharp
services.ConfigureExcos<WeatherForecastOptions>("Forecast");
```

4\. Configure Excos experiment via configuration or code (see tests project for examples).

```csharp
services.ConfigureExcosFeatures("Features");
```

```json
{
    "Features": {
        "Forecast": {
            "Variants": {
                "Celsius": {
                    "Allocation": "100%",
                    "Settings": {
                        "Forecast": {
                            "TemperatureScale": "Celsius"
                        }
                    }
                },
                "Fahrenheit": {
                    "Allocation": "100%",
                    "Filters": {
                        "Country": "US"
                    },
                    "Settings": {
                        "Forecast": {
                            "TemperatureScale": "Fahrenheit"
                        }
                    }
                }
            }
        }
    }
}
```

5\. Inject `IContextualOptions` into your service.

```csharp
internal class WeatherForecast
{
    public DateTime Date { get; set; }
    public int Temperature { get; set; }
    public string TemperatureScale { get; set; } = string.Empty;
}

internal class WeatherForecastService
{
    private readonly IContextualOptions<WeatherForecastOptions, WeatherForecastContext> _contextualOptions;
    private readonly Random _rng = new(0);

    public WeatherForecastService(IContextualOptions<WeatherForecastOptions, WeatherForecastContext> contextualOptions)
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

## Usages

* Experiments

  Configure variants with disjoint allocation ranges (e.g. `[0;0.5)` and `[0.5;1]`).
* Feature rollouts

  Configure a single variant with percentage allocation (e.g. `10%`, later updated to `25%`, etc).
* Feature gates

  Configure a single variant with 100% allocation and focus on filters to deliver the feature to the right audience.

## Filters

* Match string (config: a string with no `*`)
* Match regex (config: a simple string with `*` placeholders or a full on expression starting with `^`)
* In range (config: a `[ or ( xx; yy ) or ]` expression where `xx` and `yy` is either Guid, DateTimeOffset or Double)
