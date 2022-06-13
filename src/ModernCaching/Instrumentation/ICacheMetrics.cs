namespace ModernCaching.Instrumentation;

internal interface ICacheMetrics
{
    void IncrementLocalCacheGetHits();
    void IncrementLocalCacheGetMisses();
    void IncrementLocalCacheSets();
    void IncrementLocalCacheDeleteHits();
    void IncrementLocalCacheDeleteMisses();
    void UpdateLocalCacheCount(long count);

    void IncrementDistributedCacheGetHits();
    void IncrementDistributedCacheGetMisses();
    void IncrementDistributedCacheGetErrors();
    void IncrementDistributedCacheSets();
    void IncrementDistributedCacheDeletes();

    void IncrementDataSourceLoadOks();
    void IncrementDataSourceLoadErrors();
    void IncrementDataSourceKeyLoadHits(long value = 1);
    void IncrementDataSourceKeyLoadMisses(long value = 1);
    void IncrementDataSourceKeyLoadErrors(long value = 1);
}
