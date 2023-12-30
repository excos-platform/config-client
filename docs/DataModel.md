# Data Model

This document briefly describes the data model for features/experiments used in Excos.

A **unit** is either a user ID or a session ID, or some other semi-persistent identifier for which we will compute experiment allocation.

An **experiment** is described by name and **variants**, each of which holds allocation information.

An **allocation** is the partitioning of the unit identifier space into segments. For 50/50 allocation we create two segments: `[0, 0.5)` and `[0.5, 1)`. In order to evenly distribute identifiers over the 0-1 range we use hashing. To ensure each experiment is independent we add salt to the hashing. You may override the salt if for example you want to create two experiments which must not overlap.

An experiment is configured with filters to limit the population it affects. Filters are usually user centric and can talk about their properties, such as country of origin or age. Variants can have additional filters. Basic filters would allow wildcards (*). Regex filters would start with `^`. A filter can be represented by an array of values/expressions.

Each variant specifies settings it provides if the user falls into its allocation.

Given the context T we look through all experiments that match T over filtering conditions. We hash the identifier and pick all variants for which the allocation range matches. We select all variant for which filtering conditions are met. If there's more than variant selected we use priority setting or pick the one with most filter rules.

```json
{
    "experiments": {
        "exp1": {
            "enabled": true,
            "filters": {
                "country": ["US", "UK"]
            },
            "allocationUnit": "userId",
            "salt": "nvjdn6z8",
            "variants": {
                "A": {
                    "allocation": "[0; 0.5)",
                    "settings": {
                        "MyOption": {
                            "Color": "Blue"
                        }
                    }
                },
                "B": {
                    "allocation": "[0.5; 1)",
                    "settings": {
                        "MyOption": {
                            "Color": "Red"
                        }
                    }
                },
                "B_Chrome": {
                    "allocation": "[0.5; 1)",
                    "filters": {
                        "browser": "*Chrome*"
                    },
                    "settings": {
                        "MyOption": {
                            "Color": "Red",
                            "Offset": 5
                        }
                    }
                }
            }
        }
    }
}
```