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
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appsettings)))
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.ConfigureExcos<TestOptions>("Test");
        services.ConfigureExcosFeatures("Features");

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var contextualOptions = provider.GetRequiredService<IContextualOptions<TestOptions>>();

        var options = await contextualOptions.GetAsync(new ContextWithIdentifier
        {
            Identifier = "user1",
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
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appsettings)))
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.ConfigureExcos<TestOptions>("Test");
        services.ConfigureExcosFeatures("Features");

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var contextualOptions = provider.GetRequiredService<IContextualOptions<TestOptions>>();

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
        public FeatureMetadata? Metadata { get; set; }
    }
}
