// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Contextual;
using Microsoft.Extensions.Options.Contextual.Provider;
using Xunit;

namespace Excos.Options.Tests;

public class DictionaryOptionsContextTests
{
    [Fact]
    public void PopulateReceiver_WithValues_ReceivesAllKeyValuePairs()
    {
        var context = new DictionaryOptionsContext(new Dictionary<string, string>
        {
            ["Market"] = "US",
            ["Environment"] = "Production",
            ["UserId"] = "user123"
        });

        var receiver = new TestReceiver();
        context.PopulateReceiver(receiver);

        Assert.Equal(3, receiver.ReceivedValues.Count);
        Assert.Equal("US", receiver.ReceivedValues["Market"]);
        Assert.Equal("Production", receiver.ReceivedValues["Environment"]);
        Assert.Equal("user123", receiver.ReceivedValues["UserId"]);
    }

    [Fact]
    public void PopulateReceiver_EmptyDictionary_ReceivesNothing()
    {
        var context = new DictionaryOptionsContext(new Dictionary<string, string>());

        var receiver = new TestReceiver();
        context.PopulateReceiver(receiver);

        Assert.Empty(receiver.ReceivedValues);
    }

    private class TestReceiver : IOptionsContextReceiver
    {
        public Dictionary<string, string?> ReceivedValues { get; } = new();

        public void Receive<T>(string key, T value)
        {
            ReceivedValues[key] = value?.ToString();
        }
    }
}
