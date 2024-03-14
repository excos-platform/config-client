# Excos - .NET feature management and experimentation

This library aims to provide experiment configuration framework on top of [Microsoft.Extensions.Options.Contextual](https://www.nuget.org/packages/Microsoft.Extensions.Options.Contextual) ([source](https://github.com/dotnet/extensions/tree/main/src/Libraries/Microsoft.Extensions.Options.Contextual)) and guide you through the steps to set up experiments in your application.

I got inspired to start working on this after writing [a blog post on experimentation](https://devblog.dziubiak.pl/web/02-experimentation/).

> **excos** - groups of people appointed or elected as the decision-making body of an organization

| Package | Version |
| ------- | ------- |
| Excos.Options            | [![NuGet version (Excos.Options)](https://img.shields.io/nuget/v/Excos.Options.svg)](https://www.nuget.org/packages/Excos.Options/) |
| Excos.Options.GrowthBook | [![NuGet version (Excos.Options.GrowthBook)](https://img.shields.io/nuget/v/Excos.Options.GrowthBook.svg)](https://www.nuget.org/packages/Excos.Options.GrowthBook/) |

## Usage

See [Usage](docs/Usage.md) for more details and unit tests for code samples.

```csharp
services.ConfigureExcos<MyOptions>("MySection");
services.ConfigureExcosFeatures("Features");

var contextualOptions = provider.GetRequiredService<IContextualOptions<MyOptions>>();
var options = await contextualOptions.GetAsync(new MyContext { UserId = "deadbeef" }, default);
// options.MyValue
```

## Roadmap

1. ✔️Experiment data model implementation and contextual options loader. With options based provider.
2. ✔️Extension methods for fluent experiment setup (over options provider).
3. ~~EF Core based provider~~.
4. ✔️GrowthBook integration (configuration provider + experiments provider).

Check out the [GrowthBook integration guide](docs/GrowthBookGuide.md).
