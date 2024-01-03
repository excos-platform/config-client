// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Filtering;

namespace Excos.Options.GrowthBook;

internal static class FilterParser
{
    public static Dictionary<string, IFilteringCondition> ParseFilters(JsonElement conditions)
    {
        var filters = new Dictionary<string, IFilteringCondition>();
        if (conditions.ValueKind == JsonValueKind.Object)
        {
            foreach (var condition in conditions.EnumerateObject())
            {
                var key = condition.Name;
                var value = condition.Value;
                filters.Add(key, ParseCondition(value));
            }
        }

        return filters;
    }

    private static IFilteringCondition ParseCondition(JsonElement condition)
    {
        if (condition.ValueKind == JsonValueKind.Object)
        {
            var properties = condition.EnumerateObject().ToList();
            if (properties.Count == 1)
            {
                return ParseConditionOperator(properties[0]);
            }
            else
            {
                return new AndFilter(properties.Select(ParseConditionOperator));
            }
        }
        else if (condition.ValueKind == JsonValueKind.Number)
        {
            return new ComparisonNumberFilter(r => r == 0, condition.GetDouble());
        }
        else if (condition.ValueKind == JsonValueKind.String)
        {
            return new ComparisonStringFilter(r => r == 0, condition.GetString()!);
        }
        else if (condition.ValueKind == JsonValueKind.Array)
        {
            var values = condition.EnumerateArray().Select(v => v.GetString()!).ToList();
            return new InFilter(values);
        }
        else if (condition.ValueKind == JsonValueKind.True)
        {
            return new ComparisonBoolFilter(r => r == 0, true);
        }
        else if (condition.ValueKind == JsonValueKind.False)
        {
            return new ComparisonBoolFilter(r => r == 0, false);
        }

        return NeverFilteringCondition.Instance;
    }

    private static IFilteringCondition ParseConditionOperator(JsonProperty property)
    {
        Version? version;
        if (property.Name == "$exists")
        {
            if (property.Value.ValueKind == JsonValueKind.False)
            {
                return new NotFilter(new ExistsFilter());
            }
            else if (property.Value.ValueKind == JsonValueKind.True)
            {
                return new ExistsFilter();
            }
        }
        else if (property.Name == "$not")
        {
            return new NotFilter(ParseCondition(property.Value));
        }
        else if (property.Name == "$and")
        {
            return new AndFilter(property.Value.EnumerateArray().Select(ParseCondition));
        }
        else if (property.Name == "$or")
        {
            return new OrFilter(property.Value.EnumerateArray().Select(ParseCondition));
        }
        else if (property.Name == "$nor")
        {
            return new NotFilter(new OrFilter(property.Value.EnumerateArray().Select(ParseCondition)));
        }
        else if (property.Name == "$in")
        {
            return new InFilter(property.Value.EnumerateArray().Select(v => v.GetString()!));
        }
        else if (property.Name == "$nin")
        {
            return new NotFilter(new InFilter(property.Value.EnumerateArray().Select(v => v.GetString()!)));
        }
        else if (property.Name == "$all")
        {
            return new AllFilter(property.Value.EnumerateArray().Select(ParseCondition));
        }
        else if (property.Name == "$elemMatch")
        {
            return new ElemMatchFilter(ParseCondition(property.Value));
        }
        else if (property.Name == "$size")
        {
            return new SizeFilter(property.Value.GetInt32());
        }
        else if (property.Name == "$gt")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r > 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r > 0, property.Value.GetString()!);
        }
        else if (property.Name == "$gte")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r >= 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r >= 0, property.Value.GetString()!);
        }
        else if (property.Name == "$lt")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r < 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r < 0, property.Value.GetString()!);
        }
        else if (property.Name == "$lte")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r <= 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r <= 0, property.Value.GetString()!);
        }
        else if (property.Name == "$eq")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r == 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r == 0, property.Value.GetString()!);
        }
        else if (property.Name == "$ne")
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
                return new ComparisonNumberFilter(r => r != 0, property.Value.GetDouble());
            else if (property.Value.ValueKind == JsonValueKind.String)
                return new ComparisonStringFilter(r => r != 0, property.Value.GetString()!);
        }
        else if (property.Name == "$regex")
        {
            return new RegexFilteringCondition(property.Value.GetString()!);
        }
        else if (property.Name == "$vgt" && ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r > 0, version);
        }
        else if (property.Name == "$vgte" && ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r >= 0, version);
        }
        else if (property.Name == "$vlt" && ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r < 0, version); ;
        }
        else if (property.Name == "$vlte" && ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r <= 0, version);
        }
        else if (property.Name == "$veq" && ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r == 0, version);
        }
        else if (property.Name == "$vne" && ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version))
        {
            return new ComparisonVersionStringFilter(r => r != 0, version);
        }

        return NeverFilteringCondition.Instance;
    }
}

internal class NamespaceInclusiveFilter : IFilteringCondition
{
    private readonly string _namespaceId;
    private readonly Range<double> _range;
    private readonly IFilteringCondition? _innerCondition;

    public NamespaceInclusiveFilter(string namespaceId, Range<double> range, IFilteringCondition? innerCondition)
    {
        _namespaceId = namespaceId;
        _range = range;
        _innerCondition = innerCondition;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        var n = GrowthBookHash.V1.GetAllocationSpot($"__{_namespaceId}", value?.ToString() ?? string.Empty);
        return _range.Contains(n) && (_innerCondition?.IsSatisfiedBy(value) ?? true);
    }
}

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
        if (TryParse(value, out var version))
        {
            return _comparisonResult(version.CompareTo(_value));
        }

        return false;
    }

    public static bool TryParse<T>(T input, [NotNullWhen(true)] out Version? version) => Version.TryParse(Regex.Replace(input?.ToString() ?? string.Empty, "(^v|\\+.*$)", "").Replace('-', '.'), out version);
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
