// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Contextual;
using Microsoft.Extensions.Options.Contextual;
using Xunit;

namespace Excos.Options.Tests.Contextual;

public class ContextReceiverTests
{
    [Fact]
    public void Receive_WithEmptyIdentifier_ReturnsTheSameAllocation()
    {
        ContextWithIdentifier context1 = new();
        ContextReceiver receiver1 = PopulateReceiver(context1);
        ContextWithIdentifier context2 = new();
        ContextReceiver receiver2 = PopulateReceiver(context2);

        var allocationSpot1 = receiver1.GetIdentifierAllocationSpot(string.Empty);
        var allocationSpot2 = receiver2.GetIdentifierAllocationSpot(string.Empty);

        Assert.Equal(allocationSpot1, allocationSpot2);
    }

    [Fact]
    public void Receive_WithAnyIdProperty_ReturnsTheSameAllocation()
    {
        const string id = "abc";
        ContextWithIdentifier context1 = new() { Identifier = id };
        ContextReceiver receiver1 = PopulateReceiver(context1);
        ContextWithIdentifier context2 = new() { UserId = id };
        ContextReceiver receiver2 = PopulateReceiver(context2);
        ContextWithIdentifier context3 = new() { SessionId = id };
        ContextReceiver receiver3 = PopulateReceiver(context3);

        var allocationSpot1 = receiver1.GetIdentifierAllocationSpot(string.Empty);
        var allocationSpot2 = receiver2.GetIdentifierAllocationSpot(string.Empty);
        var allocationSpot3 = receiver3.GetIdentifierAllocationSpot(string.Empty);

        Assert.Equal(allocationSpot1, allocationSpot2);
        Assert.Equal(allocationSpot1, allocationSpot3);
    }

    [Fact]
    public void Receive_WithTwoIdProperties_IfTheFirstIsTheSame_ReturnsTheSameAllocation()
    {
        // IMPORTANT
        // Properties are populated in the receiver in alphabetical order
        const string id = "abc", id2 = "cde", id3 = "efg";
        ContextWithIdentifier context1 = new() { SessionId = id, UserId = id2 };
        ContextReceiver receiver1 = PopulateReceiver(context1);
        ContextWithIdentifier context2 = new() { SessionId = id, UserId = id3 };
        ContextReceiver receiver2 = PopulateReceiver(context2);

        var allocationSpot1 = receiver1.GetIdentifierAllocationSpot(string.Empty);
        var allocationSpot2 = receiver2.GetIdentifierAllocationSpot(string.Empty);

        Assert.Equal(allocationSpot1, allocationSpot2);
    }

    private static ContextReceiver PopulateReceiver<TContext>(TContext context) where TContext : IOptionsContext
    {
        ContextReceiver receiver = new();
        context.PopulateReceiver(receiver);
        return receiver;
    }
}
