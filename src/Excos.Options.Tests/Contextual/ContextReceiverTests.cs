// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Contextual;
using Microsoft.Extensions.Options.Contextual;
using Xunit;

namespace Excos.Options.Tests.Contextual;

public class ContextReceiverTests
{
    /// <summary>
    /// Ensure there is no randomness when handling the same identifier.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void Allocation_Receive_WithSameIdentifier_ReturnsTheSameAllocation(string value)
    {
        ContextWithIdentifier context1 = new() { Identifier = value };
        AllocationContextReceiver receiver1 = PopulateAllocationReceiver(context1, nameof(context1.Identifier));
        ContextWithIdentifier context2 = new() { Identifier = value };
        AllocationContextReceiver receiver2 = PopulateAllocationReceiver(context2, nameof(context2.Identifier));

        var allocationSpot1 = receiver1.GetIdentifierAllocationSpot();
        var allocationSpot2 = receiver2.GetIdentifierAllocationSpot();

        Assert.Equal(allocationSpot1, allocationSpot2);
    }

    [Fact]
    public void Allocation_Receive_WithAnyIdProperty_ReturnsTheSameAllocation()
    {
        const string id = "abc";
        ContextWithIdentifier context1 = new() { Identifier = id };
        AllocationContextReceiver receiver1 = PopulateAllocationReceiver(context1, nameof(context1.Identifier));
        ContextWithIdentifier context2 = new() { UserId = id };
        AllocationContextReceiver receiver2 = PopulateAllocationReceiver(context2, nameof(context2.UserId));
        ContextWithIdentifier context3 = new() { SessionId = id };
        AllocationContextReceiver receiver3 = PopulateAllocationReceiver(context3, nameof(context3.SessionId));

        var allocationSpot1 = receiver1.GetIdentifierAllocationSpot();
        var allocationSpot2 = receiver2.GetIdentifierAllocationSpot();
        var allocationSpot3 = receiver3.GetIdentifierAllocationSpot();

        Assert.Equal(allocationSpot1, allocationSpot2);
        Assert.Equal(allocationSpot1, allocationSpot3);
    }

    [Fact]
    public void Receive_WhenAskedAboutNonExistentProperty_ReturnsSameAllocationAsEmpty()
    {
        ContextWithIdentifier context1 = new() { Identifier = "x", UserId = "y", SessionId = "z"};
        AllocationContextReceiver receiver1 = PopulateAllocationReceiver(context1, "AnonymousId");
        ContextWithIdentifier context2 = new();
        AllocationContextReceiver receiver2 = PopulateAllocationReceiver(context2, nameof(context2.UserId));

        var allocationSpot1 = receiver1.GetIdentifierAllocationSpot();
        var allocationSpot2 = receiver2.GetIdentifierAllocationSpot();

        Assert.Equal(allocationSpot1, allocationSpot2);
    }

    private static AllocationContextReceiver PopulateAllocationReceiver<TContext>(TContext context, string propertyName) where TContext : IOptionsContext
    {
        AllocationContextReceiver receiver = new(propertyName, salt: string.Empty);
        context.PopulateReceiver(receiver);
        return receiver;
    }
}
