using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModernCaching.DataSource;
using ModernCaching.DistributedCaching;
using ModernCaching.Instrumentation;
using ModernCaching.LocalCaching;
using ModernCaching.Utils;

namespace ModernCaching
{
    /// <summary>
    /// Builder for <see cref="IReadOnlyCache{TKey,TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of the keys in the cache. This type should implement <see cref="object.Equals(object)"/>
    /// (and optionally <see cref="IEquatable{T}"/> if it's a struct) and <see cref="object.GetHashCode"/> .
    /// </typeparam>
    /// <typeparam name="TValue">
    /// The type of the values in the cache. This type can optionally implement <see cref="object.Equals(object)"/>
    /// (and <see cref="IEquatable{T}"/> if it's a struct) to avoid setting the local cache when the value didn't
    /// change. That reduces the gen 2 fragmentation.
    /// </typeparam>
    public class ReadOnlyCacheBuilder<TKey, TValue> where TKey : notnull
    {
        private readonly string _name;
        private readonly IDataSource<TKey, TValue> _dataSource;
        private readonly ReadOnlyCacheOptions _options;

        private ICache<TKey, TValue>? _localCache;
        private IAsyncCache? _distributedCache;
        private IKeyValueSerializer<TKey, TValue>? _keyValueSerializer;
        private string? _keyPrefix;
        private Func<object?, Task<IEnumerable<TKey>>>? _getKeys;
        private object? _getKeysState;
        private ILoggerFactory? _loggerFactory;

        /// <summary>
        /// Initializes a new instance of <see cref="ReadOnlyCacheBuilder{TKey,TValue}"/> class.
        /// </summary>
        /// <param name="name">Name of cache. Used in the distributed cache key, logging and metrics.</param>
        /// <param name="dataSource">Source of the data.</param>
        /// <param name="options">Extra options to control the cache.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="dataSource"/> is null.</exception>
        public ReadOnlyCacheBuilder(string name, IDataSource<TKey, TValue> dataSource, ReadOnlyCacheOptions? options = null)
        {
            CheckTypeOverrideEqualsAndGetHashCode(typeof(TKey));
            _name = NormalizeCacheName(name ?? throw new ArgumentNullException(nameof(name)));
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _options = options ?? new ReadOnlyCacheOptions();
        }

        /// <summary>
        /// Adds a local cache to the resulting <see cref="IReadOnlyCache{TKey,TValue}"/>.
        /// </summary>
        /// <param name="localCache">The local cache.</param>
        /// <exception cref="ArgumentNullException"><see cref="localCache"/> is null.</exception>
        /// <returns>A reference to this instance.</returns>
        public ReadOnlyCacheBuilder<TKey, TValue> WithLocalCache(ICache<TKey, TValue> localCache)
        {
            _localCache = localCache ?? throw new ArgumentNullException(nameof(localCache));
            return this;
        }

        /// <summary>
        /// Adds a distributed cache to the resulting <see cref="IReadOnlyCache{TKey,TValue}"/> with a way to convert the
        /// <typeparamref name="TValue"/> to string and the <typeparamref name="TValue"/> to bytes.
        /// </summary>
        /// <param name="distributedCache">The distributed cache.</param>
        /// <param name="keyValueSerializer"><typeparamref name="TKey"/>/<typeparamref name="TValue"/> serializer.</param>
        /// <param name="keyPrefix">Prefix prepended to the distributed cache key.</param>
        /// <exception cref="ArgumentNullException"><paramref name="distributedCache"/> or <paramref name="keyValueSerializer"/> is null.</exception>
        /// <returns>A reference to this instance.</returns>
        public ReadOnlyCacheBuilder<TKey, TValue> WithDistributedCache(IAsyncCache distributedCache,
            IKeyValueSerializer<TKey, TValue> keyValueSerializer, string? keyPrefix = null)
        {
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _keyValueSerializer = keyValueSerializer ?? throw new ArgumentNullException(nameof(keyValueSerializer));
            _keyPrefix = keyPrefix;
            return this;
        }

        /// <summary>
        /// Specifies the function to get the keys to preload in <see cref="BuildAsync"/>.
        /// </summary>
        /// <param name="getKeys">A function to get the keys to preload.</param>
        /// <param name="state">An object containing information to be used by the <paramref name="getKeys"/> method, or null.</param>
        /// <exception cref="ArgumentNullException"><paramref name="getKeys"/> is null.</exception>
        /// <returns>A reference to this instance.</returns>
        public ReadOnlyCacheBuilder<TKey, TValue> WithPreload(Func<object?, Task<IEnumerable<TKey>>> getKeys,
            object? state)
        {
            _getKeys = getKeys ?? throw new ArgumentNullException(nameof(getKeys));
            _getKeysState = state;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="ILoggerFactory"/> that will be used to create <see cref="ILogger"/> instances for
        /// logging done by this cache.
        /// </summary>
        /// <param name="loggerFactory">The logger factory to be used.</param>
        /// <exception cref="ArgumentNullException"><paramref name="loggerFactory"/> is null.</exception>
        /// <returns>A reference to this instance.</returns>
        public ReadOnlyCacheBuilder<TKey, TValue> WithLoggerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw  new ArgumentNullException(nameof(loggerFactory));
            return this;
        }

        /// <summary>
        /// Builds and preloads the <see cref="IReadOnlyCache{TKey,TValue}"/>. If <see cref="WithPreload"/> was not used,
        /// this method will return synchronously.
        /// </summary>
        /// <returns>The built <see cref="IReadOnlyCache{TKey,TValue}"/>.</returns>
        public async Task<IReadOnlyCache<TKey, TValue>> BuildAsync()
        {
            OpenTelemetryCacheMetrics metrics = new(_name);

            ILogger? localCacheLogger = _loggerFactory?.CreateLogger<ICache<TKey, TValue>>();
            var localCache = _localCache != null ? new InstrumentedCache<TKey, TValue>(_localCache, metrics, localCacheLogger) : null;

            ILogger? distributedCacheLogger = _loggerFactory?.CreateLogger<IAsyncCache>();
            var distributedCache = _distributedCache != null ? new InstrumentedAsyncCache(_distributedCache, metrics, distributedCacheLogger) : null;

            ILogger? dataSourceLogger = _loggerFactory?.CreateLogger<IDataSource<TKey, TValue>>();
            var dataSource = new InstrumentedDataSource<TKey, TValue>(_dataSource, metrics, dataSourceLogger);

            ILogger? distributedCacheWrapperLogger = _loggerFactory?.CreateLogger<IDistributedCache<TKey, TValue>>();
            IDistributedCache<TKey, TValue>? distributedCacheWrapper = distributedCache != null
                ? new DistributedCache<TKey, TValue>(_name, distributedCache, _keyValueSerializer!, _keyPrefix, distributedCacheWrapperLogger)
                : null;

            var cache = new ReadOnlyCache<TKey, TValue>(_name, localCache, distributedCacheWrapper, dataSource,
                _options, UtilsCache.LoadingTimer, UtilsCache.DateTime, UtilsCache.Random);

            if (_getKeys != null)
            {
                var keys = await _getKeys(_getKeysState);
                await cache.LoadAsync(keys);
            }

            return cache;
        }

        private string NormalizeCacheName(string name)
        {
            return Regex.Replace(name, "[^a-zA-Z0-9.\\-_]", "");
        }

        private void CheckTypeOverrideEqualsAndGetHashCode(Type type)
        {
            void CheckTypeOverrideMethod(string methodName, Type[] types)
            {
                if (type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, types!, null)!
                    .DeclaringType == typeof(object))
                {
                    throw new ArgumentException($"Argument type '{type}' doesn't override method '{methodName}");
                }
            }

            CheckTypeOverrideMethod(nameof(Equals), new[] { typeof(object) });
            CheckTypeOverrideMethod(nameof(GetHashCode), Array.Empty<Type>());
        }
    }
}
