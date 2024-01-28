using System.Collections.Generic;
using System.Diagnostics.Metrics;
using ModernCaching.Utils;

namespace ModernCaching.Telemetry;

internal sealed class CacheMetrics : ICacheMetrics
{
    private const string MetricNamePrefix = "modern_caching";

    private readonly HighReadLowWriteCounter _localCacheGetHits;
    private readonly HighReadLowWriteCounter _localCacheGetMisses;
    private readonly HighReadLowWriteCounter _localCacheSets;
    private readonly HighReadLowWriteCounter _localCacheDeleteHits;
    private readonly HighReadLowWriteCounter _localCacheDeleteMisses;
    private readonly HighReadLowWriteCounter _distributedCacheGetHits;
    private readonly HighReadLowWriteCounter _distributedCacheGetMisses;
    private readonly HighReadLowWriteCounter _distributedCacheGetErrors;
    private readonly HighReadLowWriteCounter _distributedCacheSets;
    private readonly HighReadLowWriteCounter _distributedCacheDeletes;
    private readonly HighReadLowWriteCounter _dataSourceLoadOks;
    private readonly HighReadLowWriteCounter _dataSourceLoadErrors;
    private readonly HighReadLowWriteCounter _dataSourceKeyLoadHits;
    private readonly HighReadLowWriteCounter _dataSourceKeyLoadMisses;
    private readonly HighReadLowWriteCounter _dataSourceKeyLoadErrors;

    // ReSharper disable NotAccessedField.Local
    private readonly ObservableCounter<long> _localCacheRequestsCounter;
    private readonly ObservableGauge<long> _localCacheCountGauge;
    private readonly ObservableCounter<long> _distributedCacheRequestsCounter;
    private readonly ObservableCounter<long> _dataSourceLoadsCounter;
    private readonly ObservableCounter<long> _dataSourceKeyLoadsCounter;
    // ReSharper restore NotAccessedField.Local

    private long _localCacheCount;

    public CacheMetrics(string cacheName)
    {
        _localCacheGetHits = new HighReadLowWriteCounter();
        _localCacheGetMisses = new HighReadLowWriteCounter();
        _localCacheSets = new HighReadLowWriteCounter();
        _localCacheDeleteHits = new HighReadLowWriteCounter();
        _localCacheDeleteMisses = new HighReadLowWriteCounter();
        _distributedCacheGetHits = new HighReadLowWriteCounter();
        _distributedCacheGetMisses = new HighReadLowWriteCounter();
        _distributedCacheGetErrors = new HighReadLowWriteCounter();
        _distributedCacheSets = new HighReadLowWriteCounter();
        _distributedCacheDeletes = new HighReadLowWriteCounter();
        _dataSourceLoadOks = new HighReadLowWriteCounter();
        _dataSourceLoadErrors = new HighReadLowWriteCounter();
        _dataSourceKeyLoadHits = new HighReadLowWriteCounter();
        _dataSourceKeyLoadMisses = new HighReadLowWriteCounter();
        _dataSourceKeyLoadErrors = new HighReadLowWriteCounter();

        KeyValuePair<string, object?> getOperationTag = new("operation", "get");
        KeyValuePair<string, object?> setOperationTag = new("operation", "set");
        KeyValuePair<string, object?> deleteOperationTag = new("operation", "del");
        KeyValuePair<string, object?> okStatusTag = new("status", "ok");
        KeyValuePair<string, object?> hitStatusTag = new("status", "hit");
        KeyValuePair<string, object?> missStatusTag = new("status", "miss");
        KeyValuePair<string, object?> errorStatusTag = new("status", "error");

        KeyValuePair<string, object?> cacheNameTag = new("name", cacheName);
        KeyValuePair<string, object?>[] localCacheGetHitsTags = { cacheNameTag, getOperationTag, hitStatusTag };
        KeyValuePair<string, object?>[] localCacheGetMissesTags = { cacheNameTag, getOperationTag, missStatusTag };
        KeyValuePair<string, object?>[] localCacheSetsTags = { cacheNameTag, setOperationTag, okStatusTag };
        KeyValuePair<string, object?>[] localCacheDeleteHitsTags = { cacheNameTag, deleteOperationTag, hitStatusTag };
        KeyValuePair<string, object?>[] localCacheDeleteMissesTags = { cacheNameTag, deleteOperationTag, missStatusTag };
        KeyValuePair<string, object?>[] localCacheCountTags = { cacheNameTag };
        KeyValuePair<string, object?>[] distributedCacheGetHitsTags = { cacheNameTag, getOperationTag, hitStatusTag };
        KeyValuePair<string, object?>[] distributedCacheGetMissesTags = { cacheNameTag, getOperationTag, missStatusTag };
        KeyValuePair<string, object?>[] distributedCacheGetErrorsTags = { cacheNameTag, getOperationTag, errorStatusTag };
        KeyValuePair<string, object?>[] distributedCacheSetsTags = { cacheNameTag, setOperationTag, okStatusTag };
        KeyValuePair<string, object?>[] distributedCacheDeletesTags = { cacheNameTag, deleteOperationTag, hitStatusTag };
        KeyValuePair<string, object?>[] dataSourceLoadOksTags = { cacheNameTag, okStatusTag };
        KeyValuePair<string, object?>[] dataSourceLoadErrorsTags = { cacheNameTag, errorStatusTag };
        KeyValuePair<string, object?>[] dataSourceKeyLoadHitsTags = { cacheNameTag, hitStatusTag };
        KeyValuePair<string, object?>[] dataSourceKeyLoadMissesTags = { cacheNameTag, missStatusTag };
        KeyValuePair<string, object?>[] dataSourceKeyLoadErrorsTags = { cacheNameTag, errorStatusTag };

        var meter = UtilsCache.Meter;
        _localCacheRequestsCounter = meter.CreateObservableCounter($"{MetricNamePrefix}.local_cache.requests",
            () => new[]
            {
                new Measurement<long>(_localCacheGetHits.Value, localCacheGetHitsTags),
                new Measurement<long>(_localCacheGetMisses.Value, localCacheGetMissesTags),
                new Measurement<long>(_localCacheSets.Value, localCacheSetsTags),
                new Measurement<long>(_localCacheDeleteHits.Value, localCacheDeleteHitsTags),
                new Measurement<long>(_localCacheDeleteMisses.Value, localCacheDeleteMissesTags),
            }, description: "Local cache request statuses by operation");
        _localCacheCountGauge = meter.CreateObservableGauge($"{MetricNamePrefix}.local_cache.count", () =>
                new Measurement<long>(_localCacheCount, localCacheCountTags),
            description: "Local cache entries count");
        _distributedCacheRequestsCounter = meter.CreateObservableCounter(
            $"{MetricNamePrefix}.distributed_cache.requests", () => new[]
            {
                new Measurement<long>(_distributedCacheGetHits.Value, distributedCacheGetHitsTags),
                new Measurement<long>(_distributedCacheGetMisses.Value, distributedCacheGetMissesTags),
                new Measurement<long>(_distributedCacheGetErrors.Value, distributedCacheGetErrorsTags),
                new Measurement<long>(_distributedCacheSets.Value, distributedCacheSetsTags),
                new Measurement<long>(_distributedCacheDeletes.Value, distributedCacheDeletesTags),
            }, description: "Distributed cache request statuses by operation");
        _dataSourceLoadsCounter = meter.CreateObservableCounter($"{MetricNamePrefix}.data_source.loads",
            () => new[]
            {
                new Measurement<long>(_dataSourceLoadOks.Value, dataSourceLoadOksTags),
                new Measurement<long>(_dataSourceLoadErrors.Value, dataSourceLoadErrorsTags),
            }, description: "Data source load statuses");
        _dataSourceKeyLoadsCounter = meter.CreateObservableCounter($"{MetricNamePrefix}.data_source.key_loads",
            () => new[]
            {
                new Measurement<long>(_dataSourceKeyLoadHits.Value, dataSourceKeyLoadHitsTags),
                new Measurement<long>(_dataSourceKeyLoadMisses.Value, dataSourceKeyLoadMissesTags),
                new Measurement<long>(_dataSourceKeyLoadErrors.Value, dataSourceKeyLoadErrorsTags),
            }, description: "Data source load statuses for each requested key");
    }

    public void IncrementLocalCacheGetHits() => _localCacheGetHits.Increment();
    public void IncrementLocalCacheGetMisses() => _localCacheGetMisses.Increment();
    public void IncrementLocalCacheSets() => _localCacheSets.Increment();
    public void IncrementLocalCacheDeleteHits() => _localCacheDeleteHits.Increment();
    public void IncrementLocalCacheDeleteMisses() => _localCacheDeleteMisses.Increment();
    public void UpdateLocalCacheCount(long count) => _localCacheCount = count;
    public void IncrementDistributedCacheGetHits() => _distributedCacheGetHits.Increment();
    public void IncrementDistributedCacheGetMisses() => _distributedCacheGetMisses.Increment();
    public void IncrementDistributedCacheGetErrors() => _distributedCacheGetErrors.Increment();
    public void IncrementDistributedCacheSets() => _distributedCacheSets.Increment();
    public void IncrementDistributedCacheDeletes() => _distributedCacheDeletes.Increment();
    public void IncrementDataSourceLoadOks() => _dataSourceLoadOks.Increment();
    public void IncrementDataSourceLoadErrors() => _dataSourceLoadErrors.Increment();
    public void IncrementDataSourceKeyLoadHits(long value) => _dataSourceKeyLoadHits.Add(value);
    public void IncrementDataSourceKeyLoadMisses(long value) => _dataSourceKeyLoadMisses.Add(value);
    public void IncrementDataSourceKeyLoadErrors(long value) => _dataSourceKeyLoadErrors.Add(value);
}
