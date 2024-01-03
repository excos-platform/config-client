// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections;
using System.Runtime.CompilerServices;
using Excos.Options.Abstractions;

namespace Excos.Options.GrowthBook;

internal class OrFilter : IFilteringCondition
{
    private readonly IEnumerable<IFilteringCondition> _conditions;

    public OrFilter(IEnumerable<IFilteringCondition> conditions)
    {
        _conditions = conditions;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        foreach (var condition in _conditions)
        {
            if (condition.IsSatisfiedBy(value))
            {
                return true;
            }
        }

        return false;
    }
}

internal class AndFilter : IFilteringCondition
{
    private readonly IEnumerable<IFilteringCondition> _conditions;

    public AndFilter(IEnumerable<IFilteringCondition> conditions)
    {
        _conditions = conditions;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        foreach (var condition in _conditions)
        {
            if (!condition.IsSatisfiedBy(value))
            {
                return false;
            }
        }

        return true;
    }
}

internal class NotFilter : IFilteringCondition
{
    private readonly IFilteringCondition _condition;

    public NotFilter(IFilteringCondition condition)
    {
        _condition = condition;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        return !_condition.IsSatisfiedBy(value);
    }
}

internal class ExistsFilter : IFilteringCondition
{
    public bool IsSatisfiedBy<T>(T value)
    {
        return value != null;
    }
}

internal class InFilter : IFilteringCondition
{
    private readonly IEnumerable<string> _values;

    public InFilter(IEnumerable<string> values)
    {
        _values = values;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        if (value is IEnumerable<string> strings)
        {
            foreach (var elem in strings)
            {
                if (_values.Contains(elem))
                {
                    return true;
                }
            }

            return false;
        }
        else
        {
            return _values.Contains(value?.ToString() ?? string.Empty);
        }
    }
}

internal class ComparisonBoolFilter : IFilteringCondition
{
    private readonly Func<int?, bool> _comparisonResult;
    private readonly bool _value;

    public ComparisonBoolFilter(Func<int?, bool> comparisonResult, bool value)
    {
        _comparisonResult = comparisonResult;
        _value = value;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        if (typeof(T) == typeof(bool))
        {
            return _comparisonResult(Unsafe.As<T, bool>(ref value).CompareTo(_value));
        }

        return false;
    }
}

internal class ComparisonStringFilter : IFilteringCondition
{
    private readonly Func<int?, bool> _comparisonResult;
    private readonly string _value;

    public ComparisonStringFilter(Func<int?, bool> comparisonResult, string value)
    {
        _comparisonResult = comparisonResult;
        _value = value;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        return _comparisonResult(string.Compare(value?.ToString(), _value, StringComparison.InvariantCultureIgnoreCase));
    }
}

internal class ComparisonVersionStringFilter : IFilteringCondition
{
    private readonly Func<int?, bool> _comparisonResult;
    private readonly Version _value;

    public ComparisonVersionStringFilter(Func<int?, bool> comparisonResult, Version value)
    {
        _comparisonResult = comparisonResult;
        _value = value;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        if (Version.TryParse((value?.ToString() ?? string.Empty).AsSpan().TrimStart('v'), out var version))
        {
            return _comparisonResult(version.CompareTo(_value));
        }

        return false;
    }
}

internal class ComparisonNumberFilter : IFilteringCondition
{
    private readonly Func<int?, bool> _comparisonResult;
    private readonly double _value;

    public ComparisonNumberFilter(Func<int?, bool> comparisonResult, double value)
    {
        _comparisonResult = comparisonResult;
        _value = value;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        if (value is double d)
        {
            return _comparisonResult(d.CompareTo(_value));
        }
        else if (value is float f)
        {
            return _comparisonResult(f.CompareTo(_value));
        }
        else if (value is int i)
        {
            return _comparisonResult(i.CompareTo(_value));
        }
        else if (value is uint u)
        {
            return _comparisonResult(u.CompareTo(_value));
        }
        else if (value is short s)
        {
            return _comparisonResult(s.CompareTo(_value));
        }
        else if (value is ushort us)
        {
            return _comparisonResult(us.CompareTo(_value));
        }

        return false;
    }
}

internal class ElemMatchFilter : IFilteringCondition
{
    private readonly IFilteringCondition _condition;

    public ElemMatchFilter(IFilteringCondition condition)
    {
        _condition = condition;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        if (value is IEnumerable<string> strings)
        {
            foreach (var elem in strings)
            {
                if (_condition.IsSatisfiedBy(elem))
                {
                    return true;
                }
            }
        }

        if (value is IEnumerable<double> doubles)
        {
            foreach (var elem in doubles)
            {
                if (_condition.IsSatisfiedBy(elem))
                {
                    return true;
                }
            }
        }

        if (value is IEnumerable<int> ints)
        {
            foreach (var elem in ints)
            {
                if (_condition.IsSatisfiedBy(elem))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

internal class SizeFilter : IFilteringCondition
{
    private readonly int _size;

    public SizeFilter(int size)
    {
        _size = size;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        if (value is IEnumerable<string> strings)
        {
            return strings.Count() == _size;
        }

        if (value is IEnumerable<double> doubles)
        {
            return doubles.Count() == _size;
        }

        if (value is IEnumerable<int> ints)
        {
            return ints.Count() == _size;
        }

        if (value is ICollection objects)
        {
            return objects.Count == _size;
        }

        return false;
    }
}

internal class AllFilter : IFilteringCondition
{
    private readonly IEnumerable<IFilteringCondition> _conditions;

    public AllFilter(IEnumerable<IFilteringCondition> conditions)
    {
        _conditions = conditions;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        if (value is IEnumerable<string> strings)
        {
            foreach (var condition in _conditions)
            {
                bool any = false;
                foreach (var elem in strings)
                {
                    if (!condition.IsSatisfiedBy(elem))
                    {
                        any = true;
                    }
                }

                if (!any)
                {
                    return false;
                }
            }
        }

        if (value is IEnumerable<double> doubles)
        {
            foreach (var condition in _conditions)
            {
                bool any = false;
                foreach (var elem in doubles)
                {
                    if (!condition.IsSatisfiedBy(elem))
                    {
                        any = true;
                    }
                }

                if (!any)
                {
                    return false;
                }
            }
        }

        if (value is IEnumerable<int> ints)
        {
            foreach (var condition in _conditions)
            {
                bool any = false;
                foreach (var elem in ints)
                {
                    if (!condition.IsSatisfiedBy(elem))
                    {
                        any = true;
                    }
                }

                if (!any)
                {
                    return false;
                }
            }
        }

        return false;
    }
}
