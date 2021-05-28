﻿using System;
using System.Collections.Generic;
using System.Linq;
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
using ModernCaching.LocalCaching.InMemory;

namespace ModernCaching.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<LocalGetBenchmark>();
            // BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(Array.Empty<string>(), new DebugInProcessConfig());
        }
    }

    [MemoryDiagnoser]
    public class LocalGetBenchmark
    {
        private static readonly KeyValuePair<int, int>[] Data =
        {
            new (12, 999),
            new (34, 888),
            new (56, 777),
            new (78, 666),
            new (90, 555),
        };

        private readonly IReadOnlyCache<int, int> _modernCache;
        private readonly ICacheStack _cacheTower;
        private readonly IEasyCachingProvider _easyCache;

        public LocalGetBenchmark()
        {
            _modernCache = CreateModernCache();
            _cacheTower = CreateCacheTower();
            _easyCache = CreateEasyCache();
        }

        [Benchmark]
        public int ModernCaching()
        {
            int sum = 0;
            foreach (var d in Data)
            {
                _modernCache.TryGet(d.Key, out int val);
                sum += val;
            }

            return sum;
        }

        [Benchmark]
        public async ValueTask<int> CacheTower()
        {
            int sum = 0;
            foreach (var d in Data)
            {
                sum += (await _cacheTower.GetAsync<int>(d.Key.ToString()))!.Value;
            }

            return sum;
        }

        [Benchmark]
        public int EasyCaching()
        {
            int sum = 0;
            foreach (var d in Data)
            {
                sum += _easyCache.Get<int>(d.Key.ToString()).Value;
            }

            return sum;
        }

        private IReadOnlyCache<int, int> CreateModernCache()
        {
            return new ReadOnlyCacheBuilder<int, int>("benchmark_cache", new ModernCacheDataSource())
                .WithLocalCache(new InMemoryCache<int, int>())
                .WithPreload(_ => Task.FromResult(Data.Select(d => d.Key)), null)
                .BuildAsync().GetAwaiter().GetResult();
        }

        private ICacheStack CreateCacheTower()
        {
            CacheStack cache = new (new ICacheLayer[]
            {
                new MemoryCacheLayer(),
            }, Array.Empty<ICacheExtension>());
            foreach ((int key, int value) in Data)
            {
                cache.SetAsync(key.ToString(), value, TimeSpan.FromHours(1));
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
                }, new InMemoryOptions())
            }, Array.Empty<IRedisCachingProvider>());
            var cache = easyCacheProviderFactory.GetCachingProvider("easycache");
            foreach ((int key, int value) in Data)
            {
                cache.Set(key.ToString(), value, TimeSpan.FromHours(1));
            }

            return cache;
        }

        class ModernCacheDataSource : IDataSource<int, int>
        {
            public async IAsyncEnumerable<DataSourceResult<int, int>> LoadAsync(IEnumerable<int> keys, CancellationToken cancellationToken)
            {
                foreach ((int key, int value) in Data)
                {
                    yield return new DataSourceResult<int, int>(key, value, TimeSpan.FromHours(1));
                }
            }
        }
    }
}
