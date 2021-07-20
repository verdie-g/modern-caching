using System;

namespace ModernCaching.Instrumentation
{
    internal interface ICacheMetrics
    {
        void IncrementLocalCacheGetHits();
        void IncrementLocalCacheGetMisses();
        void IncrementLocalCacheSet();
        void IncrementLocalCacheDelete();
        void SetLocalCacheCountPoller(Func<double> poller);

        void IncrementDistributedCacheGetHits();
        void IncrementDistributedCacheGetMisses();
        void IncrementDistributedCacheGetErrors();
        void IncrementDistributedCacheSet();
        void IncrementDistributedCacheDelete();

        void IncrementDataSourceLoadOk();
        void IncrementDataSourceLoadError();
        void IncrementDataSourceKeyLoadHits(long value = 1);
        void IncrementDataSourceKeyLoadMisses(long value = 1);
        void IncrementDataSourceKeyLoadErrors(long value = 1);
    }
}
