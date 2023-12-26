// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using System.Runtime.CompilerServices;
using Excos.Options.Abstractions;
using Excos.Options.Abstractions.Data;

namespace Excos.Options.Filtering;

public class RangeFilteringCondition<F> : IFilteringCondition
    where F : IComparable<F>, ISpanParsable<F>
{
    private readonly Range<F> _range;

    public RangeFilteringCondition(Range<F> range)
    {
        _range = range;
    }

    public bool IsSatisfiedBy<T>(T value)
    {
        if (typeof(T) == typeof(F))
        {
            return _range.Contains(Unsafe.As<T, F>(ref value));
        }
        else if (typeof(T).IsAssignableTo(typeof(F)) && value != null)
        {
            return _range.Contains((F)(object)value);
        }
        else if (TryQuickConvertNumber(value, out F val))
        {
            return _range.Contains(val);
        }
        else if (F.TryParse(value?.ToString(), null, out F? parsed))
        {
            return _range.Contains(parsed);
        }

        return false;
    }

    private bool TryQuickConvertNumber<T>(T value, out F val)
    {
        // special casing for double ranges as that's what will be primarily done from configuration
        if (typeof(F) == typeof(double))
        {
            double d;
            if (typeof(T) == typeof(byte))
            {
                d = Unsafe.As<T, byte>(ref value);
                val = Unsafe.As<double, F>(ref d);
                return true;
            }
            if (typeof(T) == typeof(sbyte))
            {
                d = Unsafe.As<T, sbyte>(ref value);
                val = Unsafe.As<double, F>(ref d);
                return true;
            }
            if (typeof(T) == typeof(short))
            {
                d = Unsafe.As<T, short>(ref value);
                val = Unsafe.As<double, F>(ref d);
                return true;
            }
            if (typeof(T) == typeof(ushort))
            {
                d = Unsafe.As<T, ushort>(ref value);
                val = Unsafe.As<double, F>(ref d);
                return true;
            }
            if (typeof(T) == typeof(int))
            {
                d = Unsafe.As<T, int>(ref value);
                val = Unsafe.As<double, F>(ref d);
                return true;
            }
            if (typeof(T) == typeof(uint))
            {
                d = Unsafe.As<T, uint>(ref value);
                val = Unsafe.As<double, F>(ref d);
                return true;
            }
            if (typeof(T) == typeof(long))
            {
                d = Unsafe.As<T, long>(ref value);
                val = Unsafe.As<double, F>(ref d);
                return true;
            }
            if (typeof(T) == typeof(ulong))
            {
                d = Unsafe.As<T, ulong>(ref value);
                val = Unsafe.As<double, F>(ref d);
                return true;
            }
            if (typeof(T) == typeof(float))
            {
                d = Unsafe.As<T, float>(ref value);
                val = Unsafe.As<double, F>(ref d);
                return true;
            }
        }

        val = default!;
        return false;
    }
}
