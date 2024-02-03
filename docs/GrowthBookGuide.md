# Guide to setting up an experiment with GrowthBook

In this step-by-step guide we will look at enriching the ASP .NET Core [eShopOnWeb](https://github.com/dotnet-architecture/eShopOnWeb) demo application with experimentation capability of Excos. The demo application is quite basic but it can be used to demonstrate how to get started with experimentation.

First, let's talk about choosing the right unit of experimentation. There are two main units you'd work with on the web: session and user. The benefit of choosing a user as your experiment allocation unit is the consistency of the same user seeing the same experience across multiple sessions. If you have a page that is only available after the user signs in, prefer user based experiments. However, if the page is also available while the user is not authenticated it would be weird if it changed after they sign in. In those cases you would prefer a session based experiment. The session could be identified just by dropping a cookie with a guid identifier into the user's browser.

Code changes are published at https://github.com/manio143/eShopOnWeb/tree/excosDemo

## Integrating demo with Contextual Options

For this demo we will try to create an experiment of increasing the number of items on the main page of the shop. Our experiment hypothesis is that when there's more items on one page then users don't need to click 'Next' that often which leads to less browsing fatigue and increases the average amount of products in the basket.

For this demo you need to install the NuGet package `Excos.Options.GrowthBook`. However, the first part is about integrating with `Microsoft.Extensions.Options.Contextual` and is actually independent of Excos.

I started off with duplicating the number of products in the catalog by adding 'Premium' items with a price 10$ more. To do so I have edited the `CatalogContextSeed.GetPreconfiguredItems()` method.

Next I needed to parameterize the number of items shown on a page. Initially that value is set to 10 and is in the `Constants` static class. It's being referenced by `Index` page. At the time of writing it was also used to `CachedCatalogViewModelService.GetCatalogItems`, but it should be replaced with the method's parameter `itemsPage`. So we have just one place to integrate Contextual Options into.

Let's create an options model for our index page

```csharp
public class CatalogDisplayOptions
{
    public int ItemsPerPage { get; set; } = Constants.ITEMS_PER_PAGE;
}
```

Next let's create a new contextual options context

```csharp
[OptionsContext]
public partial struct StoreOptionsContext
{
    public string? SessionId { get; set; }
}
```

For this example we will inject `IContextualOptions<CatalogDisplayOptions>` directly into the `IndexModel` class. We will create an extension method over the `HttpContext` to get (or create) a session identifier.

```csharp
public static class ContextualExtensions
{
    public static string GetOrCreateExperimentSession(this HttpContext ctx)
    {
        const string cookieName = "eShopExp";
        string? sessionId;
        if (!ctx.Request.Cookies.TryGetValue(cookieName, out sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
            ctx.Response.Cookies.Append(cookieName, sessionId);
        }

        return sessionId;
    }

    public static StoreOptionsContext ExtractStoreOptionsContext(this HttpContext ctx)
    {
        return new StoreOptionsContext { SessionId = ctx.GetOrCreateExperimentSession() };
    }
}
```

And modify the index model to retrieve the options

```csharp
public class IndexModel : PageModel
{
    private readonly ICatalogViewModelService _catalogViewModelService;
    private readonly IContextualOptions<CatalogDisplayOptions> _contextualOptions;

    public IndexModel(
        ICatalogViewModelService catalogViewModelService,
        IContextualOptions<CatalogDisplayOptions> contextualOptions)
    {
        _catalogViewModelService = catalogViewModelService;
        _contextualOptions = contextualOptions;
    }

    public required CatalogIndexViewModel CatalogModel { get; set; } = new CatalogIndexViewModel();

    public async Task OnGet(CatalogIndexViewModel catalogModel, int? pageId)
    {
        var options = await _contextualOptions.GetAsync(HttpContext.ExtractStoreOptionsContext(), default);
        CatalogModel = await _catalogViewModelService.GetCatalogItems(pageId ?? 0, options.ItemsPerPage, catalogModel.BrandFilterApplied, catalogModel.TypesFilterApplied);
    }
}
```

## Adding Excos as a source of contextual options

Excos library is a layer on top Contextual Options which provides a Feature/Experiment definition schema. We will use it to populate `CatalogDisplayOptions`. So we're going to add the following line to `Program.cs`

```csharp
builder.Services.ConfigureExcos<CatalogDisplayOptions>("CatalogDisplay");
```

The string parameter to `ConfigureExcos` is the configuration section name that will be used to bind configuration to this options instance. When we set up the experiment values later this section name will be used.

Optionally, before integrating with Growthbook, you can test the Excos feature definition is working by describing a feature rollout using builder API:

```csharp
builder.Services.BuildFeature("TestRollout")
    .Configure(feature => feature.AllocationUnit = nameof(StoreOptionsContext.SessionId))
    .Rollout<CatalogDisplayOptions>(75 /*percent*/, (options, _) => options.ItemsPerPage = 20)
    .Save();
```

## Configuring connection to GrowthBook instance

```csharp
// TODO
```

## Setting up the experiment in GrowthBook

First, we're going to add `SessionId` as an identifier to match our options context. Go to "SDK Configuration" > "Attributes" and add a new attribute.

![Create sessionId attribute](images/growthbook-demo-create-sessionid-attribute.png)

Go to "Features" tab in GrowthBook and create a new feature definition. For the value type select JSON. Excos generally expects JSON settings in the same format as used by `appsettings.json` with the `Microsoft.Extensions.Configuration` framework.

Default value:

```json
{
  "CatalogDisplay": {
    "ItemsPerPage": 10
  }
}
```

![Create feature screen](images/growthbook-demo-create-feature.png)

Next we're going to create an experiment. Scroll down on the feature page and click the Add Experiment Rule button.

![Add experiment button](images/growthbook-demo-create-experiment-button.png)

Try use clear and descriptive names for your experiments. In this example I use `Items-On-Page-10-vs-20` as the name to signal what the experiment will change. The tracking key is a unique identifier which we will later add to telemetry to signal that the session was part of this experiment.

![Create experiment](images/growthbook-demo-create-experiment-defintion.png)

Excos supports most basic GrowthBook attribute filters (targeting) - file an issue if you see something doesn't work. We're not going to use them in this demo, but generally it's quite easy to add an attribute to your options context.

## Saving experiment results

In this demo we will create a new telemetry pipeline for the purpose of processing the results with GrowthBook. When the user checks out we will log the details of their basket, the session ID and the experiment metadata. This will later allow GrowthBook's statistical engine to process the data and tell us if the change of items per page had actually influenced the desired metric.

We'll reuse the EntityFramework set up of the eShop.

_TODO_
