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
        services.AddOptions<TestOptions>().BindConfiguration("Test");
        services.ConfigureExcos<TestOptions>("Test");
        services.ConfigureExcosFeatures("Features");

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var contextualOptions = provider.GetRequiredService<IContextualOptions<TestOptions>>();
        
        var options = await contextualOptions.GetAsync(new ContextWithIdentifier
        {
            UserId = "user1",
            SessionId = "d48d716f-6e85-4eb5-a81f-dd8d14472832",
        }, default);

        Assert.Equal(2, options.Size);
        Assert.Equal("G", options.Label);
    }

    private class TestOptions
    {
        public int Size { get; set; }
        public string Label { get; set; } = string.Empty;
        public FeatureMetadata? Metadata { get; set; }
    }
}
