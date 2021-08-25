using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using CacheTower;
using CacheTower.Providers.Memory;
using EasyCaching.Core;
using EasyCaching.InMemory;
using ModernCaching.DataSource;
using ModernCaching.LocalCaching;
using ZiggyCreatures.Caching.Fusion;

namespace ModernCaching.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
#if DEBUG
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(Array.Empty<string>(), new DebugInProcessConfig());
#else
            BenchmarkRunner.Run<LocalGetBenchmark>();
#endif
        }
    }

    [MemoryDiagnoser(displayGenColumns: false)]
    public class LocalGetBenchmark
    {
        private const int DataCount = 1_000;
        private static readonly KeyValuePair<Guid, int>[] Data = Enumerable.Range(0, DataCount)
            .Select(i => KeyValuePair.Create(Guid.NewGuid(), i))
            .ToArray();

        private readonly ConcurrentDictionary<Guid, int> _concurrentDictionary;
        private readonly IReadOnlyCache<Guid, int> _modernCache;
        private readonly ICacheStack _cacheTower;
        private readonly IFusionCache _fusionCache;
        private readonly IEasyCachingProvider _easyCache;

        public LocalGetBenchmark()
        {
            _concurrentDictionary = CreateConcurrentDictionary();
            _modernCache = CreateModernCache();
            _cacheTower = CreateCacheTower();
            _fusionCache = CreateFusionCache();
            _easyCache = CreateEasyCache();
        }

        [Benchmark(OperationsPerInvoke = DataCount, Baseline = true)]
        public int ConcurrentDictionary()
        {
             int sum = 0;
             foreach (var d in Data)
             {
                 _concurrentDictionary.TryGetValue(d.Key, out int val);
                 sum += val;
             }

             return sum;
        }

        [Benchmark(OperationsPerInvoke = DataCount)]
        public int ModernCaching()
        {
            int sum = 0;
            foreach (var d in Data)
            {
                _modernCache.TryPeek(d.Key, out int val);
                sum += val;
            }

            return sum;
        }

        [Benchmark(OperationsPerInvoke = DataCount)]
        public async ValueTask<int> CacheTower()
        {
            int sum = 0;
            foreach (var d in Data)
            {
                sum += (await _cacheTower.GetAsync<int>(d.Key.ToString()))!.Value;
            }

            return sum;
        }

        [Benchmark(OperationsPerInvoke = DataCount)]
        public int FusionCache()
        {
            int sum = 0;
            foreach (var d in Data)
            {
                sum += _fusionCache.TryGet<int>(d.Key.ToString());
            }

            return sum;
        }

        [Benchmark(OperationsPerInvoke = DataCount)]
        public int EasyCaching()
        {
            int sum = 0;
            foreach (var d in Data)
            {
                sum += _easyCache.Get<int>(d.Key.ToString()).Value;
            }

            return sum;
        }

        private ConcurrentDictionary<Guid, int> CreateConcurrentDictionary()
        {
            return new ConcurrentDictionary<Guid, int>(Data);
        }

        private IReadOnlyCache<Guid, int> CreateModernCache()
        {
            return new ReadOnlyCacheBuilder<Guid, int>("benchmark_cache", new ModernCacheDataSource())
                .WithLocalCache(new MemoryCache<Guid, int>())
                .WithPreload(_ => Task.FromResult(Data.Select(d => d.Key)), null)
                .BuildAsync().GetAwaiter().GetResult();
        }

        private ICacheStack CreateCacheTower()
        {
            CacheStack cache = new (new ICacheLayer[]
            {
                new MemoryCacheLayer(),
            }, Array.Empty<ICacheExtension>());
            foreach ((Guid key, int value) in Data)
            {
                cache.SetAsync(key.ToString(), value, TimeSpan.FromHours(1));
            }

            return cache;
        }

        private IFusionCache CreateFusionCache()
        {
            FusionCache cache = new(new FusionCacheOptions());
            foreach ((Guid key, int value) in Data)
            {
                cache.Set(key.ToString(), value, TimeSpan.FromHours(1));
            }

            return cache;
        }

        private IEasyCachingProvider CreateEasyCache()
        {
            DefaultEasyCachingProviderFactory easyCacheProviderFactory = new(new IEasyCachingProvider[]
            {
                new DefaultInMemoryCachingProvider("easycache", new[]
                {
                    new InMemoryCaching("easycache", new InMemoryCachingOptions()),
                }, new InMemoryOptions(), null),
            }, Array.Empty<IRedisCachingProvider>());
            var cache = easyCacheProviderFactory.GetCachingProvider("easycache");
            foreach ((Guid key, int value) in Data)
            {
                cache.Set(key.ToString(), value, TimeSpan.FromHours(1));
            }

            return cache;
        }

        class ModernCacheDataSource : IDataSource<Guid, int>
        {
#pragma warning disable 1998
            public async IAsyncEnumerable<DataSourceResult<Guid, int>> LoadAsync(IEnumerable<Guid> keys,
#pragma warning restore 1998
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                foreach ((Guid key, int value) in Data)
                {
                    yield return new DataSourceResult<Guid, int>(key, value, TimeSpan.FromHours(1));
                }
            }
        }
    }
}
