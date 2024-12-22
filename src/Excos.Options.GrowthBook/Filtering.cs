// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;
using Excos.Options.Filtering;
using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.GrowthBook;

internal interface IFilter
{
    public bool IsSatisfied(JsonElement context);
}

internal class JsonFilteringCondition : IFilteringCondition
{
    private readonly IFilter _filter;
    public JsonFilteringCondition(IFilter filter)
    {
        _filter = filter;
    }
    public bool IsSatisfiedBy<T>(T value) where T : IOptionsContext
    {
        return _filter.IsSatisfied(JsonSerializer.SerializeToElement(value));
    }
}

internal static class FilterParser
{
    public static IFilteringCondition ParseFilters(JsonElement conditions)
    {
        var filter = ParseCondition(conditions);
        return new JsonFilteringCondition(filter);
    }

    private static IFilter ParseCondition(JsonElement condition)
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
        else if (condition.ValueKind == JsonValueKind.Array)
        {
            var values = condition.EnumerateArray().Select(ParseCondition).ToList();
            return new ArrayFilter(values);
        }
        else
        {
            return new ComparisonFilter(ComparisonType.Equal, condition);
        }
    }

    private static IFilter ParseConditionOperator(JsonProperty property)
    {
        Version? version;
        switch (property.Name)
        {
            case "$exists":
                if (property.Value.ValueKind == JsonValueKind.False)
                {
                    return new NotFilter(new ExistsFilter());
                }
                else if (property.Value.ValueKind == JsonValueKind.True)
                {
                    return new ExistsFilter();
                }
                break;
            case "$not":
                return new NotFilter(ParseCondition(property.Value));
            case "$and":
                return new AndFilter(property.Value.EnumerateArray().Select(ParseCondition));
            case "$or":
                return new OrFilter(property.Value.EnumerateArray().Select(ParseCondition));
            case "$nor":
                return new NotFilter(new OrFilter(property.Value.EnumerateArray().Select(ParseCondition)));
            case "$in":
                {
                    return new InFilter(property.Value.EnumerateArray());
                }

            case "$nin":
                {
                    return new NotFilter(new InFilter(property.Value.EnumerateArray()));
                }

            case "$all":
                return new AllFilter(property.Value.EnumerateArray().Select(ParseCondition));
            case "$elemMatch":
                return new ElemMatchFilter(ParseCondition(property.Value));
            case "$size":
                return new SizeFilter(property.Value.GetInt32());
            case "$gt":
                return new ComparisonFilter(ComparisonType.GreaterThan, property.Value);
            case "$gte":
                return new ComparisonFilter(ComparisonType.GreaterThanOrEqual, property.Value);
            case "$lt":
                return new ComparisonFilter(ComparisonType.LessThan, property.Value);
            case "$lte":
                return new ComparisonFilter(ComparisonType.LessThanOrEqual, property.Value);
            case "$eq":
                return new ComparisonFilter(ComparisonType.Equal, property.Value);
            case "$ne":
                return new ComparisonFilter(ComparisonType.NotEqual, property.Value);
            case "$regex":
                return new RegexFilter(property.Value.GetString()!);
            case "$vgt" when ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version):
                {
                    return new ComparisonVersionStringFilter(r => r > 0, version);
                }

            case "$vgte" when ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version):
                {
                    return new ComparisonVersionStringFilter(r => r >= 0, version);
                }

            case "$vlt" when ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version):
                {
                    return new ComparisonVersionStringFilter(r => r < 0, version); ;
                }

            case "$vlte" when ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version):
                {
                    return new ComparisonVersionStringFilter(r => r <= 0, version);
                }

            case "$veq" when ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version):
                {
                    return new ComparisonVersionStringFilter(r => r == 0, version);
                }

            case "$vne" when ComparisonVersionStringFilter.TryParse(property.Value.GetString()!, out version):
                {
                    return new ComparisonVersionStringFilter(r => r != 0, version);
                }
            case "$type":
                return new TypeFilter(property.Value.GetString()!);
            default:
                return new PropertyFilter(property.Name, ParseCondition(property.Value));
        }
    }
}

internal class NamespaceFilteringCondition : PropertyFilteringCondition
{
    private readonly string _namespaceId;
    private readonly Range<double> _range;

    public NamespaceFilteringCondition(string allocationUnit, string namespaceId, Range<double> range)
        : base(allocationUnit)
    {
        _namespaceId = namespaceId;
        _range = range;
    }

    protected override bool PropertyPredicate<T>(T value)
    {
        var n = GrowthBookHash.V1.GetAllocationSpot($"__{_namespaceId}", value?.ToString() ?? string.Empty);
        return _range.Contains(n);
    }
}

//TODO
internal class AllocationFilteringCondition : PropertyFilteringCondition
{
    private readonly Range<double> _range;

    public AllocationFilteringCondition(string allocationUnit, Range<double> range)
        : base(allocationUnit)
    {
        _range = range;
    }

    protected override bool PropertyPredicate<T>(T value)
    {
        var n = GrowthBookHash.V2.GetAllocationSpot($"__{_namespaceId}", value?.ToString() ?? string.Empty);
        return _range.Contains(n);
    }
}

internal class OrFilter : IFilter
{
    private readonly IEnumerable<IFilter> _conditions;

    public OrFilter(IEnumerable<IFilter> conditions)
    {
        _conditions = conditions;
    }

    public bool IsSatisfied(JsonElement context)
    {
        foreach (var condition in _conditions)
        {
            if (condition.IsSatisfied(context))
            {
                return true;
            }
        }

        return false;
    }
}

internal class AndFilter : IFilter
{
    private readonly IEnumerable<IFilter> _conditions;

    public AndFilter(IEnumerable<IFilter> conditions)
    {
        _conditions = conditions;
    }

    public bool IsSatisfied(JsonElement context)
    {
        foreach (var condition in _conditions)
        {
            if (!condition.IsSatisfied(context))
            {
                return false;
            }
        }

        return true;
    }
}

internal class NotFilter : IFilter
{
    private readonly IFilter _condition;

    public NotFilter(IFilter condition)
    {
        _condition = condition;
    }

    public bool IsSatisfied(JsonElement context)
    {
        return !_condition.IsSatisfied(context);
    }
}

internal class ExistsFilter : IFilter
{
    public bool IsSatisfied(JsonElement context)
    {
        return context.ValueKind != JsonValueKind.Undefined;
    }
}

internal class InFilter : IFilter
{
    private readonly IEnumerable<JsonElement> _values;

    public InFilter(IEnumerable<JsonElement> values)
    {
        _values = values;
    }

    public bool IsSatisfied(JsonElement context)
    {
        if (context.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in context.EnumerateArray())
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
            return _values.Contains(context);
        }
    }
}

internal enum ComparisonType
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

internal class ComparisonFilter : IFilter
{
    private readonly ComparisonType _comparisonType;
    private readonly JsonElement _value;

    public ComparisonFilter(ComparisonType comparisonType, JsonElement value)
    {
        _comparisonType = comparisonType;
        _value = value;
    }

    public bool IsSatisfied(JsonElement context)
    {
        return Compare(context, _value, _comparisonType);
    }

    private static bool Compare<T>(T left, T right, ComparisonType comparisonType) where T : IComparable<T>
    {
        switch (comparisonType)
        {
            case ComparisonType.Equal:
                return left.CompareTo(right) == 0;
            case ComparisonType.NotEqual:
                return left.CompareTo(right) != 0;
            case ComparisonType.GreaterThan:
                return left.CompareTo(right) > 0;
            case ComparisonType.GreaterThanOrEqual:
                return left.CompareTo(right) >= 0;
            case ComparisonType.LessThan:
                return left.CompareTo(right) < 0;
            case ComparisonType.LessThanOrEqual:
                return left.CompareTo(right) <= 0;
            default:
                return false;
        }
    }

    private static bool Compare(JsonElement left, JsonElement right, ComparisonType comparisonType)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Number:
                return Compare(left.GetDouble(), right.GetDouble(), comparisonType);
            case JsonValueKind.String:
                return Compare(left.GetString()!, right.GetString()!, comparisonType);
            case JsonValueKind.True:
            case JsonValueKind.False:
                return Compare(left.GetBoolean(), right.GetBoolean(), comparisonType);
            case JsonValueKind.Null:
                return comparisonType == ComparisonType.Equal;
            case JsonValueKind.Undefined:
                return comparisonType == ComparisonType.NotEqual;
            case JsonValueKind.Object:
                if (comparisonType != ComparisonType.Equal && comparisonType != ComparisonType.NotEqual)
                {
                    // we cannot compare objects this way
                    return false;    
                }
                // basically we try to return true in not equal and false if equal
                var falseResponse = comparisonType == ComparisonType.NotEqual;
                foreach (var property in left.EnumerateObject())
                {
                    if (!right.TryGetProperty(property.Name, out var value))
                    {
                        return falseResponse;
                    }

                    if (!Compare(property.Value, value, ComparisonType.Equal))
                    {
                        return falseResponse;
                    }
                }
                foreach (var property in right.EnumerateObject())
                {
                    if (!left.TryGetProperty(property.Name, out var value))
                    {
                        return falseResponse;
                    }
                }
                return !falseResponse;
            case JsonValueKind.Array:
                if (comparisonType != ComparisonType.Equal && comparisonType != ComparisonType.NotEqual)
                {
                    // we cannot compare objects this way
                    return false;
                }
                // basically we try to return true in not equal and false if equal
                falseResponse = comparisonType == ComparisonType.NotEqual;
                if (left.GetArrayLength() != right.GetArrayLength())
                {
                    return falseResponse;
                }
                for (int i = 0; i < left.GetArrayLength(); i++)
                {
                    if (!Compare(left[i], right[i], ComparisonType.Equal))
                    {
                        return falseResponse;
                    }
                }
                return !falseResponse;
            default:
                return false;
        }
    }
}

// TODO rework this bitch
internal class ComparisonVersionStringFilter : IFilter
{
    private readonly ComparisonType _comparisonType;
    private readonly string _value;

    public ComparisonVersionStringFilter(ComparisonType comparisonType, string value)
    {
        _comparisonType = comparisonType;
        _value = GetPaddedVersionString(value);
    }

    public bool IsSatisfied(JsonElement context)
    {
        

        return false;
    }

    // https://docs.growthbook.io/lib/build-your-own#private-paddedversionstringinput-string
    public static string GetPaddedVersionString(string version)
    {
        // Remove build info and leading `v` if any
        // Split version into parts (both core version numbers and pre-release tags)
        // "v1.2.3-rc.1+build123" -> ["1","2","3","rc","1"]
        var parts = Regex.Replace(version, "(^v|\\+.*$)", "").Split(['.', '-'], StringSplitOptions.None);

        var builder = new StringBuilder();
        // Left pad each numeric part with spaces so string comparisons will work ("9">"10", but " 9"<"10")
        int i = 0;
        for (; i < parts.Length; i++)
        {
            if (Regex.IsMatch(parts[i], "^[0-9]+")
            {
                var padding = 5 - parts[i].Length;
                builder.Append(' ', padding > 0 ? padding : 0);
            }
            
            builder.Append(parts[i]);
            builder.Append('-');
        }

        // handle not full versions (like 1.0)
        if (i < 3)
        {
            for (; i < 3; i++)
            {
                builder.Append("    0");
                builder.Append('-');
            }
        }

        // If it's SemVer without a pre-release, add `~` to the end
        // ["1","0","0"] -> ["1","0","0","~"]
        // "~" is the largest ASCII character, so this will make "1.0.0" greater than "1.0.0-beta" for example
        if (i == 3)
        {
            builder.Append("~");
        }

        return builder.ToString();
    }
}

internal class ElemMatchFilter : IFilter
{
    private readonly IFilter _condition;

    public ElemMatchFilter(IFilter condition)
    {
        _condition = condition;
    }

    public bool IsSatisfied(JsonElement context)
    {
        if (context.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in context.EnumerateArray())
            {
                if (_condition.IsSatisfied(elem))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

internal class SizeFilter : IFilter
{
    private readonly int _size;

    public SizeFilter(int size)
    {
        _size = size;
    }

    public bool IsSatisfied(JsonElement context)
    {
        if (context.ValueKind == JsonValueKind.Array)
        {
            return context.GetArrayLength() == _size;
        }

        return false;
    }
}

internal class AllFilter : IFilter
{
    private readonly IEnumerable<IFilter> _conditions;

    public AllFilter(IEnumerable<IFilter> conditions)
    {
        _conditions = conditions;
    }

    public bool IsSatisfied(JsonElement context)
    {
        if (context.ValueKind == JsonValueKind.Array)
        {
            foreach (var condition in _conditions)
            {
                bool any = false;
                foreach (var elem in context.EnumerateArray())
                {
                    if (!condition.IsSatisfied(elem))
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

internal class ArrayFilter : IFilter
{
    private readonly IEnumerable<IFilter> _conditions;
    public ArrayFilter(IEnumerable<IFilter> conditions)
    {
        _conditions = conditions;
    }
    public bool IsSatisfied(JsonElement context)
    {
        if (context.ValueKind == JsonValueKind.Array)
        {
            var enumerator = context.EnumerateArray();
            foreach (var condition in _conditions)
            {
                if (!enumerator.MoveNext())
                {
                    return false;
                }
                if (!condition.IsSatisfied(enumerator.Current))
                {
                    return false;
                }
            }
            return !enumerator.MoveNext();
        }
        return false;
    }
}

internal class TypeFilter : IFilter
{
    private readonly string _type;
    public TypeFilter(string type)
    {
        _type = type;
    }
    public bool IsSatisfied(JsonElement context)
    {
        return context.ValueKind switch
        {
            JsonValueKind.String => _type == "string",
            JsonValueKind.Number => _type == "number",
            JsonValueKind.True or JsonValueKind.False => _type == "boolean",
            JsonValueKind.Null => _type == "null",
            JsonValueKind.Object => _type == "object",
            JsonValueKind.Array => _type == "array",
            _ => false,
        };
    }
}

internal class PropertyFilter : IFilter
{
    private readonly string[] _path;
    private readonly IFilter _condition;
    public PropertyFilter(string propertyName, IFilter condition)
    {
        _path = propertyName.Split(".");
        _condition = condition;
    }
    public bool IsSatisfied(JsonElement context)
    {
        JsonElement target = context;
        foreach (var segment in _path)
        {
            if (target.ValueKind != JsonValueKind.Object ||
                !target.TryGetProperty(segment, out target))
            {
                target = new JsonElement(); // kind = undefined
            }
        }

        return _condition.IsSatisfied(target);
    }
}

internal class RegexFilter : IFilter
{
    private readonly Regex _regex;
    public RegexFilter(string pattern)
    {
        _regex = new Regex(pattern);
    }
    public bool IsSatisfied(JsonElement context)
    {
        return _regex.IsMatch(context.GetString() ?? string.Empty);
    }
}
