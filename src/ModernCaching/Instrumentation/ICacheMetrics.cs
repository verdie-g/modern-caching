﻿namespace ModernCaching.Instrumentation
{
    internal interface ICacheMetrics
    {
        void IncrementLocalCacheGetHits();
        void IncrementLocalCacheGetMisses();
        void IncrementLocalCacheSet();
        void IncrementLocalCacheRemove();

        void IncrementDistributedCacheGetHits();
        void IncrementDistributedCacheGetMisses();
        void IncrementDistributedCacheGetErrors();
        void IncrementDistributedCacheSet();
        void IncrementDistributedCacheRemove();

        void IncrementDataSourceLoadOk();
        void IncrementDataSourceLoadError();
        void IncrementDataSourceKeyLoadHits(long value = 1);
        void IncrementDataSourceKeyLoadMisses(long value = 1);
        void IncrementDataSourceKeyLoadErrors(long value = 1);
    }
}
