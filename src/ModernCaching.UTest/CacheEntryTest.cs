using System;
using ModernCaching.LocalCaching;
using NUnit.Framework;

namespace ModernCaching.UTest
{
    public class CacheEntryTest
    {
        [Test]
        public void ParameterlessConstructorShouldCreateEntryWithNoValue()
        {
            CacheEntry<int> entry = new();
            Assert.IsFalse(entry.HasValue);
            Assert.Throws<InvalidOperationException>(() => _ = entry.Value);
            Assert.AreEqual(default(int), entry.GetValueOrDefault());
        }

        [Test]
        public void ConstructorWithParameterShouldCreateEntryWithValue()
        {
            CacheEntry<int> entry = new(5);
            Assert.IsTrue(entry.HasValue);
            Assert.AreEqual(5, entry.Value);
            Assert.AreEqual(5, entry.GetValueOrDefault());
        }

        [Test]
        public void EqualsShouldIgnoreTimes()
        {
            CacheEntry<int> entry1 = new(5)
            {
                CreationTime = DateTime.Parse("01/02/2000"),
                ExpirationTime = DateTime.Parse("02/03/2000"),
                EvictionTime = DateTime.Parse("04/05/2000"),
            };
            CacheEntry<int> entry2 = new(5)
            {
                CreationTime = DateTime.Parse("06/07/2000"),
                ExpirationTime = DateTime.Parse("08/09/2000"),
                EvictionTime = DateTime.Parse("10/11/2000"),
            };

            Assert.AreEqual(entry1, entry2);
        }

        [Test]
        public void NoValueEntriesShouldBeEqual()
        {
            CacheEntry<int> entry1 = new();
            CacheEntry<int> entry2 = new();

            Assert.AreEqual(entry1, entry2);
        }

        [Test]
        public void NoValueAndValueEntriesShouldNotBeEqual()
        {
            CacheEntry<int> entry1 = new();
            CacheEntry<int> entry2 = new(5);

            Assert.AreNotEqual(entry1, entry2);
        }

        [Test]
        public void GetHashCodeOnNoValueShouldZero()
        {
            CacheEntry<int> entry = new();

            Assert.Zero(entry.GetHashCode());
        }

        [Test]
        public void ToStringOnNoValueShouldReturnEmpty()
        {
            CacheEntry<int> entry = new();

            Assert.IsEmpty(entry.ToString());
        }
    }
}
