// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Excos.Options.Abstractions;
using Microsoft.Extensions.Options.Contextual;
using Microsoft.Extensions.Options.Contextual.Provider;

namespace Excos.Options.Filtering;

internal abstract class PropertyFilteringCondition : IFilteringCondition
{
    private readonly string _propertyName;

    public PropertyFilteringCondition(string propertyName)
    {
        _propertyName = propertyName;
    }

    public bool IsSatisfiedBy<TContext>(TContext value) where TContext : IOptionsContext
    {
        var receiver = new PropertyValueReceiver(_propertyName, this);
        value.PopulateReceiver(receiver);
        return receiver.IsSatisfied;
    }

    protected abstract bool PropertyPredicate<T>(T value);

    private class PropertyValueReceiver : IOptionsContextReceiver
    {
        private readonly string _propertyName;
        private readonly PropertyFilteringCondition _condition;

        // defaults to false - if no property we expect is received
        public bool IsSatisfied { get; private set; }

        public PropertyValueReceiver(string propertyName, PropertyFilteringCondition condition)
        {
            _propertyName = propertyName;
            _condition = condition;
        }

        public void Receive<T>(string key, T value)
        {
            if (key != null && key.Equals(_propertyName, StringComparison.OrdinalIgnoreCase))
            {
                IsSatisfied = _condition.PropertyPredicate(value);
            }
        }
    }
}
