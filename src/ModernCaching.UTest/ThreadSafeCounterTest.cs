using System;
using System.Threading;
using System.Threading.Tasks;
using ModernCaching.Utils;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class ThreadSafeCounterTest
{
    [Test]
    public void TestThreadSafety()
    {
        const int iterations = 1_000_000;
        HighReadLowWriteCounter counter = new();

        Barrier b = new(Math.Min(4, Environment.ProcessorCount));
        Parallel.For(0, b.ParticipantCount, _ =>
        {
            b.SignalAndWait();

            for (int i = 0; i < iterations; i += 1)
            {
                counter.Increment();
                counter.Add(2);
                counter.Decrement();
            }
        });

        Assert.That(counter.Value, Is.EqualTo(iterations * b.ParticipantCount * 2));
    }
}