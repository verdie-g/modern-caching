using System;
using ModernCaching.DataSource;
using ModernCaching.DistributedCaching;
using ModernCaching.LocalCaching;

namespace ModernCaching
{
    /// <summary>
    /// Builder for <see cref="IReadOnlyCache{TKey,TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the cache.</typeparam>
    /// <typeparam name="TValue">The type of the values in the cache.</typeparam>
    public class ReadOnlyCacheBuilder<TKey, TValue> where TKey : IEquatable<TKey>
    {
        private readonly string _name;
        private readonly IDataSource<TKey, TValue> _dataSource;

        private ICache<TKey, TValue>? _localCache;
        private IAsyncCache? _distributedCache;
        private IKeyValueSerializer<TKey, TValue>? _keyValueSerializer;
        private string? _keyPrefix;

        /// <summary>
        /// Initializes a new instance of <see cref="ReadOnlyCacheBuilder{TKey,TValue}"/> class.
        /// </summary>
        /// <param name="name">Name of cache. Used in the distributed cache key, logging and metrics.</param>
        /// <param name="dataSource">Source of the data.</param>
        /// <exception cref="ArgumentNullException"><see cref="name"/> or <see cref="dataSource"/> is null.</exception>
        public ReadOnlyCacheBuilder(string name, IDataSource<TKey, TValue> dataSource)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        }

        /// <summary>
        /// Adds a local cache to the resulting <see cref="IReadOnlyCache{TKey,TValue}"/>.
        /// </summary>
        /// <param name="localCache">The local cache.</param>
        /// <exception cref="ArgumentNullException"><see cref="localCache"/> is null.</exception>
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
        /// <exception cref="ArgumentNullException"><see cref="distributedCache"/> or <see cref="keyValueSerializer"/> is null.</exception>
        public ReadOnlyCacheBuilder<TKey, TValue> WithDistributedCache(IAsyncCache distributedCache,
            IKeyValueSerializer<TKey, TValue> keyValueSerializer, string? keyPrefix = null)
        {
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _keyValueSerializer = keyValueSerializer ?? throw new ArgumentNullException(nameof(keyValueSerializer));
            _keyPrefix = keyPrefix;
            return this;
        }

        /// <summary>
        /// Builds the <see cref="IReadOnlyCache{TKey,TValue}"/>.
        /// </summary>
        public IReadOnlyCache<TKey, TValue> Build()
        {
            return new ReadOnlyCache<TKey, TValue>(_name, _localCache, _distributedCache, _keyValueSerializer,
                _keyPrefix, _dataSource);
        }
    }
}
