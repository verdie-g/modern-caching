﻿using System;
using System.Collections.Generic;

namespace ModernCaching.LocalCaching;

/// <summary>
/// Entry of an <see cref="ICache{TKey,TValue}"/>.
/// </summary>
public sealed class CacheEntry<TValue>
{
    private readonly TValue _value;

    /// <summary>Instantiates a <see cref="CacheEntry{TValue}"/> with a value.</summary>
    internal CacheEntry(TValue value)
    {
        HasValue = true;
        _value = value;
    }

    /// <summary>Instantiates a <see cref="CacheEntry{TValue}"/> without a value.</summary>
    internal CacheEntry()
    {
        HasValue = false;
        _value = default!;
    }

    /// <summary>Whether the current <see cref="CacheEntry{TValue}"/> has a value.</summary>
    public bool HasValue { get; }

    /// <summary>The value of the current entry if <see cref="HasValue"/> is true; otherwise an exception.</summary>
    /// <exception cref="InvalidOperationException"><see cref="HasValue"/> is false.</exception>
    public TValue Value => HasValue
        ? _value
        : throw new InvalidOperationException("Cache entry object must have a value.");

    /// <summary>The UTC creation time of the entry.</summary>
    public DateTime CreationTime { get; internal set; }

    /// <summary>Duration after which the entry is considered stale.</summary>
    public TimeSpan TimeToLive { get; internal set; }

    /// <summary>Retrieves the value or the default value of the underlying type.</summary>
    public TValue GetValueOrDefault()
    {
        return _value;
    }

    /// <summary>
    /// Determines whether the specified object is a <see cref="CacheEntry{TValue}"/> and that its
    /// <see cref="Value"/> is equal to to the one of the current object.
    /// </summary>
    /// <param name="other">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? other)
    {
        if (other is not CacheEntry<TValue> entry)
        {
            return false;
        }

        if (!HasValue)
        {
            return !entry.HasValue;
        }

        // Equals will involve boxing for structs that don't implement IEquatable.
        return entry.HasValue && EqualityComparer<TValue>.Default.Equals(Value, entry.Value);

    }

    /// <summary>Computes a hash of the entry using only the <see cref="Value"/>.</summary>
    public override int GetHashCode()
    {
        return HasValue && Value != null ? Value.GetHashCode() : 0;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (!HasValue || Value == null)
        {
            return string.Empty;
        }

        return Value.ToString() ?? string.Empty;
    }

    /// <summary>Creates a copy of the current instance.</summary>
    public CacheEntry<TValue> Clone()
    {
        CacheEntry<TValue> copy = HasValue ? new(Value) : new();
        copy.CreationTime = CreationTime;
        copy.TimeToLive = TimeToLive;
        return copy;
    }
}
