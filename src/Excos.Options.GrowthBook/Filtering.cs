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

    internal static IFilter ParseCondition(JsonElement condition)
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
        switch (property.Name)
        {
            case "$exists":
                if (property.Value.ValueKind == JsonValueKind.False)
                {
                    return new NotFilter(new ExistsFilter());
                }
                else
                {
                    return new ExistsFilter();
                }
            case "$not":
                return new NotFilter(ParseCondition(property.Value));
            case "$and":
                return new AndFilter(property.Value.EnumerateArray().Select(ParseCondition));
            case "$or":
                return new OrFilter(property.Value.EnumerateArray().Select(ParseCondition));
            case "$nor":
                return new NotFilter(new OrFilter(property.Value.EnumerateArray().Select(ParseCondition)));
            case "$in":
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    return NeverFilter.Instance;
                }
                return new InFilter(property.Value.EnumerateArray());
            case "$nin":
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    return NeverFilter.Instance;
                }
                return new NotFilter(new InFilter(property.Value.EnumerateArray()));
            case "$all":
                return new AllFilter(property.Value.EnumerateArray().Select(ParseCondition));
            case "$elemMatch":
                return new ElemMatchFilter(ParseCondition(property.Value));
            case "$size":
                return new SizeFilter(ParseCondition(property.Value));
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
            case "$vgt":
                return new ComparisonVersionStringFilter(ComparisonType.GreaterThan, property.Value.GetString() ?? string.Empty);
            case "$vgte":
                return new ComparisonVersionStringFilter(ComparisonType.GreaterThanOrEqual, property.Value.GetString() ?? string.Empty);
            case "$vlt":
                return new ComparisonVersionStringFilter(ComparisonType.LessThan, property.Value.GetString() ?? string.Empty);
            case "$vlte":
                return new ComparisonVersionStringFilter(ComparisonType.LessThanOrEqual, property.Value.GetString() ?? string.Empty);
            case "$veq":
                return new ComparisonVersionStringFilter(ComparisonType.Equal, property.Value.GetString() ?? string.Empty);
            case "$vne":
                return new ComparisonVersionStringFilter(ComparisonType.NotEqual, property.Value.GetString() ?? string.Empty);
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

internal class AllocationFilteringCondition : PropertyFilteringCondition
{
    private readonly string _salt;
    private readonly GrowthBookHash _hash;
    private readonly Allocation _range;

    public AllocationFilteringCondition(string allocationUnit, string salt, GrowthBookHash hash, Allocation range)
        : base(allocationUnit)
    {
        _salt = salt;
        _hash = hash;
        _range = range;
    }

    protected override bool PropertyPredicate<T>(T value)
    {
        var n = _hash.GetAllocationSpot(value?.ToString() ?? string.Empty, _salt);
        return _range.Contains(n);
    }
}

internal class NeverFilter : IFilter
{
    public static NeverFilter Instance = new();
    public bool IsSatisfied(JsonElement context)
    {
        return false;
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

        // return true if empty, false otherwise
        return !_conditions.Any();
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
                if (_values.Contains(elem, Comparison.ElementComparer))
                {
                    return true;
                }
            }

            return false;
        }
        else
        {
            return _values.Contains(context, Comparison.ElementComparer);
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
        return Comparison.Compare(context, _value, _comparisonType);
    }
}

internal static class Comparison
{
    // we will do case insensitive equality comparison for string values
    public static bool Compare(string left, string right, ComparisonType comparisonType)
    {
        switch (comparisonType)
        {
            case ComparisonType.Equal:
                return left.Equals(right, StringComparison.OrdinalIgnoreCase);
            case ComparisonType.NotEqual:
                return !left.Equals(right, StringComparison.OrdinalIgnoreCase);
            case ComparisonType.GreaterThan:
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase) > 0;
            case ComparisonType.GreaterThanOrEqual:
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase) >= 0;
            case ComparisonType.LessThan:
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase) < 0;
            case ComparisonType.LessThanOrEqual:
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase) <= 0;
            default:
                return false;
        }
    }
    public static bool Compare<T>(T left, T right, ComparisonType comparisonType) where T : IComparable<T>
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

    public static bool Compare(JsonElement left, JsonElement right, ComparisonType comparisonType)
    {
        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.Number
        && double.TryParse(left.GetString(), out var number))
        {
            return Compare(number, right.GetDouble(), comparisonType);
        }
        else if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.String
        && double.TryParse(right.GetString(), out number))
        {
            return Compare(left.GetDouble(), number, comparisonType);
        }
        else if ((left.ValueKind == JsonValueKind.Undefined && right.ValueKind == JsonValueKind.Null)
        || (left.ValueKind == JsonValueKind.Null && right.ValueKind == JsonValueKind.Undefined)
        && comparisonType == ComparisonType.Equal)
        {
            // special case null = undefined
            return true;
        }
        else if (left.ValueKind != right.ValueKind)
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
            default:
                return false;
        }
    }

    public static IEqualityComparer<JsonElement> ElementComparer = new JsonElementComparer();
    private class JsonElementComparer : IEqualityComparer<JsonElement>
    {
        public bool Equals(JsonElement x, JsonElement y)
            => Compare(x, y, ComparisonType.Equal);

        public int GetHashCode([DisallowNull] JsonElement obj)
            => obj.GetRawText().GetHashCode();
    }
}

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
        var value = GetPaddedVersionString(context.GetString() ?? string.Empty);
        return Comparison.Compare(value, _value, _comparisonType);
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
            if (Regex.IsMatch(parts[i], "^[0-9]+"))
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
    private readonly IFilter _inner;

    public SizeFilter(IFilter inner)
    {
        _inner = inner;
    }

    public bool IsSatisfied(JsonElement context)
    {
        if (context.ValueKind == JsonValueKind.Array)
        {
            return _inner.IsSatisfied(JsonSerializer.SerializeToElement(context.GetArrayLength()));
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
            if (target.ValueKind != JsonValueKind.Object)
            {
                target = new JsonElement(); // kind = undefined
                break;
            }

            var found = false;
            foreach (var property in target.EnumerateObject())
            {
                if (property.Name.Equals(segment, StringComparison.OrdinalIgnoreCase))
                {
                    target = property.Value;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                target = new JsonElement(); // kind = undefined
            }
        }

        return _condition.IsSatisfied(target);
    }
}

internal class RegexFilter : IFilter
{
    private readonly Regex? _regex;
    public RegexFilter(string pattern)
    {
        try
        {
            _regex = new Regex(pattern);
        }
        catch(RegexParseException)
        {
            _regex = null;
        }
    }
    public bool IsSatisfied(JsonElement context)
    {
        return _regex?.IsMatch(context.GetString() ?? string.Empty) ?? false;
    }
}
