using System;
using ModernCaching.LocalCaching;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class CacheEntryTest
{
    [Test]
    public void ParameterlessConstructorShouldCreateEntryWithNoValue()
    {
        CacheEntry<int> entry = new();
        Assert.That(entry.HasValue, Is.False);
        Assert.Throws<InvalidOperationException>(() => _ = entry.Value);
        Assert.That(entry.GetValueOrDefault(), Is.EqualTo(default(int)));
    }

    [Test]
    public void ConstructorWithParameterShouldCreateEntryWithValue()
    {
        CacheEntry<int> entry = new(5);
        Assert.That(entry.HasValue, Is.True);
        Assert.That(entry.Value, Is.EqualTo(5));
        Assert.That(entry.GetValueOrDefault(), Is.EqualTo(5));
    }

    [Test]
    public void EqualsShouldIgnoreTimes()
    {
        CacheEntry<int> entry1 = new(5)
        {
            CreationTime = DateTime.Parse("01/02/2000"),
            TimeToLive = TimeSpan.FromHours(1),
        };
        CacheEntry<int> entry2 = new(5)
        {
            CreationTime = DateTime.Parse("06/07/2000"),
            TimeToLive = TimeSpan.FromHours(2),
        };

        Assert.That(entry2, Is.EqualTo(entry1));
    }

    [Test]
    public void NoValueEntriesShouldBeEqual()
    {
        CacheEntry<int> entry1 = new();
        CacheEntry<int> entry2 = new();

        Assert.That(entry2, Is.EqualTo(entry1));
    }

    [Test]
    public void NoValueAndValueEntriesShouldNotBeEqual()
    {
        CacheEntry<int> entry1 = new();
        CacheEntry<int> entry2 = new(5);

        Assert.That(entry2, Is.Not.EqualTo(entry1));
    }

    [Test]
    public void GetHashCodeOnNoValueShouldZero()
    {
        CacheEntry<int> entry = new();

        Assert.That(entry.GetHashCode(), Is.Zero);
    }

    [Test]
    public void ToStringOnNoValueShouldReturnEmpty()
    {
        CacheEntry<int> entry = new();

        Assert.That(entry.ToString(), Is.Empty);
    }

    [Theory]
    public void CloneShouldReturnANewInstance(bool hasValue)
    {
        CacheEntry<int> entry = hasValue ? new(5) : new();
        CacheEntry<int> clone = entry.Clone();

        Assert.That(clone, Is.Not.SameAs(entry));
        Assert.That(clone.HasValue, Is.EqualTo(entry.HasValue));
        Assert.That(clone.GetValueOrDefault(), Is.EqualTo(entry.GetValueOrDefault()));
        Assert.That(clone.CreationTime, Is.EqualTo(entry.CreationTime));
        Assert.That(clone.TimeToLive, Is.EqualTo(entry.TimeToLive));
    }
}
