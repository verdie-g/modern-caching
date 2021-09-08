using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Threading;

namespace ModernCaching.Instrumentation
{
    public class OpenTelemetryCacheMetrics : ICacheMetrics
    {
        private const string MetricNamePrefix = "modern_caching";
        private static readonly AssemblyName AssemblyName = typeof(OpenTelemetryCacheMetrics).Assembly.GetName();
        private static readonly string InstrumentationName = AssemblyName.Name;
        private static readonly string InstrumentationVersion = AssemblyName.Version.ToString();
        private static readonly Meter Meter = new(InstrumentationName, InstrumentationVersion);

        private static readonly KeyValuePair<string, object?> GetOperationTag = new("operation", "get");
        private static readonly KeyValuePair<string, object?> SetOperationTag = new("operation", "set");
        private static readonly KeyValuePair<string, object?> DeleteOperationTag = new("operation", "del");
        private static readonly KeyValuePair<string, object?> OkStatusTag = new("status", "hit");
        private static readonly KeyValuePair<string, object?> HitStatusTag = new("status", "hit");
        private static readonly KeyValuePair<string, object?> MissStatusTag = new("status", "miss");
        private static readonly KeyValuePair<string, object?> ErrorStatusTag = new("status", "error");

        // ReSharper disable NotAccessedField.Local
        private readonly ObservableCounter<long> _localCacheRequestsCounter;
        private readonly ObservableGauge<long> _localCacheCountCounter;
        private readonly ObservableCounter<long> _distributedCacheRequestsCounter;
        private readonly ObservableCounter<long> _dataSourceLoadsCounter;
        private readonly ObservableCounter<long> _dataSourceKeyLoadsCounter;
        // ReSharper restore NotAccessedField.Local

        private long _localCacheGetHits;
        private long _localCacheGetMisses;
        private long _localCacheSets;
        private long _localCacheDeleteHits;
        private long _localCacheDeleteMisses;
        private long _localCacheCount;
        private long _distributedCacheGetHits;
        private long _distributedCacheGetMisses;
        private long _distributedCacheGetErrors;
        private long _distributedCacheSets;
        private long _distributedCacheDeletes;
        private long _dataSourceLoadOks;
        private long _dataSourceLoadErrors;
        private long _dataSourceKeyLoadHits;
        private long _dataSourceKeyLoadMisses;
        private long _dataSourceKeyLoadErrors;

        public OpenTelemetryCacheMetrics(string cacheName)
        {
            KeyValuePair<string, object?> cacheNameTag = new("name", cacheName);
            KeyValuePair<string, object?>[] localCacheGetHitsTags = { cacheNameTag, GetOperationTag, HitStatusTag };
            KeyValuePair<string, object?>[] localCacheGetMissesTags = { cacheNameTag, GetOperationTag, MissStatusTag };
            KeyValuePair<string, object?>[] localCacheSetsTags = { cacheNameTag, SetOperationTag };
            KeyValuePair<string, object?>[] localCacheDeleteHitsTags = { cacheNameTag, DeleteOperationTag, HitStatusTag };
            KeyValuePair<string, object?>[] localCacheDeleteMissesTags = { cacheNameTag, DeleteOperationTag, MissStatusTag };
            KeyValuePair<string, object?>[] localCacheCountTags = { cacheNameTag };
            KeyValuePair<string, object?>[] distributedCacheGetHitsTags = { cacheNameTag, GetOperationTag, HitStatusTag };
            KeyValuePair<string, object?>[] distributedCacheGetMissesTags = { cacheNameTag, GetOperationTag, MissStatusTag };
            KeyValuePair<string, object?>[] distributedCacheGetErrorsTags = { cacheNameTag, GetOperationTag, ErrorStatusTag };
            KeyValuePair<string, object?>[] distributedCacheSetsTags = { cacheNameTag, SetOperationTag };
            KeyValuePair<string, object?>[] distributedCacheDeletesTags = { cacheNameTag, DeleteOperationTag };
            KeyValuePair<string, object?>[] dataSourceLoadOksTags = { cacheNameTag, OkStatusTag };
            KeyValuePair<string, object?>[] dataSourceLoadErrorsTags = { cacheNameTag, ErrorStatusTag };
            KeyValuePair<string, object?>[] dataSourceKeyLoadHitsTags = { cacheNameTag, HitStatusTag };
            KeyValuePair<string, object?>[] dataSourceKeyLoadMissesTags = { cacheNameTag, MissStatusTag };
            KeyValuePair<string, object?>[] dataSourceKeyLoadErrorsTags = { cacheNameTag, ErrorStatusTag };

            _localCacheRequestsCounter = Meter.CreateObservableCounter($"{MetricNamePrefix}.local_cache.requests",
                () => new[]
                {
                    new Measurement<long>(Volatile.Read(ref _localCacheGetHits), localCacheGetHitsTags),
                    new Measurement<long>(Volatile.Read(ref _localCacheGetMisses), localCacheGetMissesTags),
                    new Measurement<long>(Volatile.Read(ref _localCacheSets), localCacheSetsTags),
                    new Measurement<long>(Volatile.Read(ref _localCacheDeleteHits), localCacheDeleteHitsTags),
                    new Measurement<long>(Volatile.Read(ref _localCacheDeleteMisses), localCacheDeleteMissesTags),
                }, description: "local cache request statuses by operation");
            _localCacheCountCounter = Meter.CreateObservableGauge($"{MetricNamePrefix}.local_cache.count", () =>
                    new Measurement<long>(_localCacheCount, localCacheCountTags),
                description: "local cache entries count");
            _distributedCacheRequestsCounter = Meter.CreateObservableCounter(
                $"{MetricNamePrefix}.distributed_cache.requests", () => new[]
                {
                    new Measurement<long>(Volatile.Read(ref _distributedCacheGetHits), distributedCacheGetHitsTags),
                    new Measurement<long>(Volatile.Read(ref _distributedCacheGetMisses), distributedCacheGetMissesTags),
                    new Measurement<long>(Volatile.Read(ref _distributedCacheGetErrors), distributedCacheGetErrorsTags),
                    new Measurement<long>(Volatile.Read(ref _distributedCacheSets), distributedCacheSetsTags),
                    new Measurement<long>(Volatile.Read(ref _distributedCacheDeletes), distributedCacheDeletesTags),
                }, description: "distributed cache request statuses by operation");
            _dataSourceLoadsCounter = Meter.CreateObservableCounter($"{MetricNamePrefix}.data_source.loads",
                () => new[]
                {
                    new Measurement<long>(Volatile.Read(ref _dataSourceLoadOks), dataSourceLoadOksTags),
                    new Measurement<long>(Volatile.Read(ref _dataSourceLoadErrors), dataSourceLoadErrorsTags),
                }, description: "data source load statuses");
            _dataSourceKeyLoadsCounter = Meter.CreateObservableCounter($"{MetricNamePrefix}.data_source.key_loads",
                () => new[]
                {
                    new Measurement<long>(Volatile.Read(ref _dataSourceKeyLoadHits), dataSourceKeyLoadHitsTags),
                    new Measurement<long>(Volatile.Read(ref _dataSourceKeyLoadMisses), dataSourceKeyLoadMissesTags),
                    new Measurement<long>(Volatile.Read(ref _dataSourceKeyLoadErrors), dataSourceKeyLoadErrorsTags),
                }, description: "data source load statuses for each requested key");
        }

        public void IncrementLocalCacheGetHits() => Interlocked.Increment(ref _localCacheGetHits);
        public void IncrementLocalCacheGetMisses() => Interlocked.Increment(ref _localCacheGetMisses);
        public void IncrementLocalCacheSets() => Interlocked.Increment(ref _localCacheSets);
        public void IncrementLocalCacheDeleteHits() => Interlocked.Increment(ref _localCacheDeleteHits);
        public void IncrementLocalCacheDeleteMisses() => Interlocked.Increment(ref _localCacheDeleteMisses);
        public void UpdateLocalCacheCount(long count) => _localCacheCount = count;
        public void IncrementDistributedCacheGetHits() => Interlocked.Increment(ref _distributedCacheGetHits);
        public void IncrementDistributedCacheGetMisses() => Interlocked.Increment(ref _distributedCacheGetMisses);
        public void IncrementDistributedCacheGetErrors() => Interlocked.Increment(ref _distributedCacheGetErrors);
        public void IncrementDistributedCacheSets() => Interlocked.Increment(ref _distributedCacheSets);
        public void IncrementDistributedCacheDeletes() => Interlocked.Increment(ref _distributedCacheDeletes);
        public void IncrementDataSourceLoadOks() => Interlocked.Increment(ref _dataSourceLoadOks);
        public void IncrementDataSourceLoadErrors() => Interlocked.Increment(ref _dataSourceLoadErrors);
        public void IncrementDataSourceKeyLoadHits(long value) => Interlocked.Add(ref _dataSourceKeyLoadHits, value);
        public void IncrementDataSourceKeyLoadMisses(long value) => Interlocked.Add(ref _dataSourceKeyLoadMisses, value);
        public void IncrementDataSourceKeyLoadErrors(long value) => Interlocked.Add(ref _dataSourceKeyLoadErrors, value);
    }
}
