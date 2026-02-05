// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Text;
using Excos.Options.Contextual;
using Excos.Options.Providers.Configuration;
using Excos.Options.Tests.Contextual;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options.Contextual;
using Xunit;

namespace Excos.Options.Tests;

public class ConfigurationBasedFeaturesTest
{
    [Fact]
    public async Task GetContextualOptions_FromConfiguration()
    {
        const string appsettings =
        """
        {
            "Test": {
                "Size": 1,
                "Label": "S"
            },
            "Features": {
                "Resizer": {
                    "Variants": {
                        "rollout": {
                            "Allocation": "50%",
                            "Settings": {
                                "Test": {
                                    "Size": 2
                                }
                            }
                        }
                    }
                },
                "Labeler": {
                    "Enabled": false,
                    "Salt": "abcdef",
                    "Filters": {
                        "Market": ["US", "UK"]
                    },
                    "Variants": {
                        "A": {
                            "Allocation": "[0;0.5)",
                            "Settings": {
                                "Test": {
                                    "Label": "M"
                                }
                            }
                        },
                        "B": {
                            "Allocation": "[0.5;1]",
                            "Settings": {
                                "Test": {
                                    "Label": "L"
                                }
                            }
                        }
                    }
                },
                "SessionRanger": {
                    "AllocationUnit": "SessionId",
                    "Variants": {
                        "Guids": {
                            "Allocation": "100%",
                            "Filters": {
                                "SessionId": "[d0000000-0000-0000-0000-000000000000;f0000000-0000-0000-0000-000000000000)"
                            },
                            "Settings": {
                                "Test": {
                                    "Label": "G"
                                }
                            }
                        },
                        "Dates": {
                            "Allocation": "100%",
                            "Filters": {
                                "SessionId": "[2023-01-01;2024-01-01)"
                            },
                            "Settings": {
                                "Test": {
                                    "Label": "D"
                                }
                            }
                        },
                        "Doubles": {
                            "Allocation": "100%",
                            "Filters": {
                                "SessionId": "[50;75]"
                            },
                            "Settings": {
                                "Test": {
                                    "Label": "N"
                                }
                            }
                        }
                    }
                }
            }
        }
        """;
        var provider = BuildServiceProvider(appsettings);
        var contextualOptions = provider.GetRequiredService<IContextualOptions<TestOptions, ContextWithIdentifier>>();

        var options = await contextualOptions.GetAsync(new ContextWithIdentifier
        {
            UserId = "user1",
            SessionId = "d48d716f-6e85-4eb5-a81f-dd8d14472832",
        }, default);

        Assert.Equal(2, options.Size);
        Assert.Equal("G", options.Label);
    }

    [Theory]
    [InlineData("U", 1, 5)]
    [InlineData("US", 1, 5)]
    [InlineData("US", 2, 5)]
    [InlineData("US", 3, 0)]
    [InlineData("USA", 1, 5)]
    [InlineData("UK", 1, 5)]
    [InlineData("PL", 1, 5)]
    [InlineData("C", 1, 0)]
    [InlineData("CA", 1, 5)]
    [InlineData("CAD", 1, 5)]
    [InlineData("DE", 1, 0)]
    [InlineData("DE", 0, 0)]
    public async Task ConfigurationBasedFilters_Test(string market, int ageGroup, int sizeResult)
    {
        const string appsettings =
        """
        {
            "Features": {
                "Filtered": {
                    "Salt": "abcdef",
                    "Filters": {
                        "Market": ["U*", "PL", "^C.+$"],
                        "AgeGroup": ["1", "[2;3)"]
                    },
                    "Variants": {
                        "A": {
                            "Allocation": "100%",
                            "Settings": {
                                "Test": {
                                    "Size": "5"
                                }
                            }
                        }
                    }
                }
            }
        }
        """;
        var provider = BuildServiceProvider(appsettings);
        var contextualOptions = provider.GetRequiredService<IContextualOptions<TestOptions, ContextWithIdentifier>>();

        var options = await contextualOptions.GetAsync(new ContextWithIdentifier
        {
            Market = market,
            AgeGroup = ageGroup,
        }, default);

        Assert.Equal(sizeResult, options.Size);
    }

    private class TestOptions
    {
        public int Size { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    [Fact]
    public async Task Priority_ExplicitPriority_LowerValueWins()
    {
        const string appsettings =
        """
        {
            "Features": {
                "PriorityTest": {
                    "Variants": {
                        "Low": {
                            "Allocation": "100%",
                            "Priority": 10,
                            "Settings": { "Test": { "Label": "LowPriority" } }
                        },
                        "High": {
                            "Allocation": "100%",
                            "Priority": 1,
                            "Settings": { "Test": { "Label": "HighPriority" } }
                        }
                    }
                }
            }
        }
        """;

        var provider = BuildServiceProvider(appsettings);
        var contextualOptions = provider.GetRequiredService<IContextualOptions<TestOptions, ContextWithIdentifier>>();

        var options = await contextualOptions.GetAsync(new ContextWithIdentifier { UserId = "anyone" }, default);

        Assert.Equal("HighPriority", options.Label);
    }

    [Fact]
    public async Task Priority_ImplicitPriority_MoreFiltersWins()
    {
        // Variant with more filters gets implicit higher priority (1024 - filterCount)
        const string appsettings =
        """
        {
            "Features": {
                "ImplicitPriorityTest": {
                    "Variants": {
                        "General": {
                            "Allocation": "100%",
                            "Settings": { "Test": { "Label": "General" } }
                        },
                        "Specific": {
                            "Allocation": "100%",
                            "Filters": { "Market": "US" },
                            "Settings": { "Test": { "Label": "Specific" } }
                        }
                    }
                }
            }
        }
        """;

        var provider = BuildServiceProvider(appsettings);
        var contextualOptions = provider.GetRequiredService<IContextualOptions<TestOptions, ContextWithIdentifier>>();

        var options = await contextualOptions.GetAsync(new ContextWithIdentifier { UserId = "user", Market = "US" }, default);

        Assert.Equal("Specific", options.Label);
    }

    [Fact]
    public async Task Allocation_InvalidAllocation_VariantSkipped()
    {
        const string appsettings =
        """
        {
            "Features": {
                "InvalidAllocationTest": {
                    "Variants": {
                        "Invalid": {
                            "Allocation": "notvalid",
                            "Settings": { "Test": { "Label": "Invalid" } }
                        },
                        "Valid": {
                            "Allocation": "100%",
                            "Settings": { "Test": { "Label": "Valid" } }
                        }
                    }
                }
            }
        }
        """;

        var provider = BuildServiceProvider(appsettings);
        var contextualOptions = provider.GetRequiredService<IContextualOptions<TestOptions, ContextWithIdentifier>>();

        var options = await contextualOptions.GetAsync(new ContextWithIdentifier { UserId = "user" }, default);

        Assert.Equal("Valid", options.Label);
    }

    [Fact]
    public async Task Allocation_ZeroPercent_VariantSkipped()
    {
        const string appsettings =
        """
        {
            "Features": {
                "ZeroAllocationTest": {
                    "Variants": {
                        "Zero": {
                            "Allocation": "0%",
                            "Settings": { "Test": { "Label": "Zero" } }
                        },
                        "Full": {
                            "Allocation": "100%",
                            "Settings": { "Test": { "Label": "Full" } }
                        }
                    }
                }
            }
        }
        """;

        var provider = BuildServiceProvider(appsettings);
        var contextualOptions = provider.GetRequiredService<IContextualOptions<TestOptions, ContextWithIdentifier>>();

        var options = await contextualOptions.GetAsync(new ContextWithIdentifier { UserId = "user" }, default);

        Assert.Equal("Full", options.Label);
    }

    private IServiceProvider BuildServiceProvider(string appsettings)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appsettings)))
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.ConfigureExcos<TestOptions>("Test");
        services.ConfigureExcosFeatures("Features");

        return services.BuildServiceProvider();
    }
}
