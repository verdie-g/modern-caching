using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ModernCaching.DataSource;
using ModernCaching.LocalCaching;
using NUnit.Framework;

namespace ModernCaching.ITest
{
    internal class ReadOnlyValueExample
    {
        private ReadOnlyValue<IPAddress> _myIp = default!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _myIp = await ReadOnlyValue<IPAddress>.CreateAsync("my_ip", new MyIpValueSource());
        }

        [Test, Explicit]
        public async Task GetMyIp()
        {
            var myIp = await _myIp.GetAsync();
            Assert.IsNotNull(myIp);
        }

        private class MyIpValueSource : IValueSource<IPAddress>
        {
            private readonly HttpClient _httpClient;

            public MyIpValueSource()
            {
                _httpClient = new HttpClient()
                {
                    BaseAddress = new Uri("https://api.ipify.org?format=json"),
                };
            }

            public async Task<ValueSourceResult<IPAddress>> LoadAsync(CancellationToken cancellationToken)
            {
                var ipObject = await _httpClient.GetFromJsonAsync<IPObject>("", cancellationToken: cancellationToken);
                var ip = IPAddress.Parse(ipObject!.Ip);
                return new ValueSourceResult<IPAddress>(ip, TimeSpan.FromMinutes(15));
            }

            private record IPObject(string Ip);
        }
    }

    /// <summary>
    /// Similar as <see cref="IReadOnlyCache{TKey,TValue}"/> but it only caches a single value.
    /// </summary>
    internal class ReadOnlyValue<T>
    {
        public static async Task<ReadOnlyValue<T>> CreateAsync(string name, IValueSource<T> valueSource)
        {
            var cache = await new ReadOnlyCacheBuilder<int, T>(name, new ValueDataSource<T>(valueSource))
                .WithLocalCache(new ValueCache<T>())
                .WithLoggerFactory(new ConsoleLoggerFactory())
                .WithPreload(_ => Task.FromResult<IEnumerable<int>>(new[] { 0 }), null)
                .BuildAsync();
            return new ReadOnlyValue<T>(cache);
        }

        private readonly IReadOnlyCache<int, T> _cache;

        public ReadOnlyValue(IReadOnlyCache<int, T> cache)
        {
            _cache = cache;
        }

        public T Peek()
        {
            _cache.TryPeek(0, out var value);
            return value!;
        }

        public async ValueTask<T> GetAsync()
        {
            var (_, value) = await _cache.TryGetAsync(0);
            return value!;
        }
    }

    internal record ValueSourceResult<T>(T Value, TimeSpan TimeToLive);

    internal interface IValueSource<T>
    {
        Task<ValueSourceResult<T>> LoadAsync(CancellationToken cancellationToken);
    }

    internal class ValueDataSource<T> : IDataSource<int, T>
    {
        private readonly IValueSource<T> _valueSource;

        public ValueDataSource(IValueSource<T> valueSource)
        {
            _valueSource = valueSource;
        }

        public async IAsyncEnumerable<DataSourceResult<int, T>> LoadAsync(IEnumerable<int> keys,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var (value, timeToLive) = await _valueSource.LoadAsync(cancellationToken);
            yield return new DataSourceResult<int, T>(0, value, timeToLive);
        }
    }

    internal class ValueCache<T> : ICache<int, T>
    {
        private CacheEntry<T>? _value;

        public int Count => _value != null ? 1 : 0;

        public bool TryGet(int key, out CacheEntry<T> entry)
        {
            var value = _value;
            entry = value!;
            return value != null;
        }

        public void Set(int key, CacheEntry<T> entry)
        {
            Interlocked.Exchange(ref _value, entry);
        }

        public void Delete(int key)
        {
            throw new NotImplementedException();
        }
    }
}
