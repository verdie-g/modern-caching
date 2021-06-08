using System;
using System.Diagnostics.Tracing;
using System.Threading;

namespace ModernCaching.Instrumentation
{
    [EventSource(Name = "ModernCaching")]
    internal class EventCounterCacheMetrics : EventSource, ICacheMetrics
    {
        private readonly string _cacheName;

        private IncrementingPollingCounter? _localCacheGetHitsCounter;
        private IncrementingPollingCounter? _localCacheGetMissesCounter;
        private IncrementingPollingCounter? _localCacheSetCounter;
        private IncrementingPollingCounter? _localCacheRemoveCounter;
        private IncrementingPollingCounter? _distributedCacheGetHitsCounter;
        private IncrementingPollingCounter? _distributedCacheGetMissesCounter;
        private IncrementingPollingCounter? _distributedCacheGetErrorsCounter;
        private IncrementingPollingCounter? _distributedCacheSetCounter;
        private IncrementingPollingCounter? _distributedCacheRemoveCounter;
        private IncrementingPollingCounter? _dataSourceLoadOkCounter;
        private IncrementingPollingCounter? _dataSourceLoadErrorCounter;
        private IncrementingPollingCounter? _dataSourceKeyLoadHitsCounter;
        private IncrementingPollingCounter? _dataSourceKeyLoadMissesCounter;
        private IncrementingPollingCounter? _dataSourceKeyLoadErrorsCounter;

        private long _localCacheGetHits;
        private long _localCacheGetMisses;
        private long _localCacheSet;
        private long _localCacheRemove;
        private long _distributedCacheGetHits;
        private long _distributedCacheGetMisses;
        private long _distributedCacheGetErrors;
        private long _distributedCacheSet;
        private long _distributedCacheRemove;
        private long _dataSourceLoadOk;
        private long _dataSourceLoadError;
        private long _dataSourceKeyLoadHits;
        private long _dataSourceKeyLoadMisses;
        private long _dataSourceKeyLoadErrors;

        public EventCounterCacheMetrics(string cacheName) => _cacheName = cacheName;

        public void IncrementLocalCacheGetHits() => Interlocked.Increment(ref _localCacheGetHits);
        public void IncrementLocalCacheGetMisses() => Interlocked.Increment(ref _localCacheGetMisses);
        public void IncrementLocalCacheSet() => Interlocked.Increment(ref _localCacheSet);
        public void IncrementLocalCacheRemove() => Interlocked.Increment(ref _localCacheRemove);
        public void IncrementDistributedCacheGetHits() => Interlocked.Increment(ref _distributedCacheGetHits);
        public void IncrementDistributedCacheGetMisses() => Interlocked.Increment(ref _distributedCacheGetMisses);
        public void IncrementDistributedCacheGetErrors() => Interlocked.Increment(ref _distributedCacheGetErrors);
        public void IncrementDistributedCacheSet() => Interlocked.Increment(ref _distributedCacheSet);
        public void IncrementDistributedCacheRemove() => Interlocked.Increment(ref _distributedCacheRemove);
        public void IncrementDataSourceLoadOk() => Interlocked.Increment(ref _dataSourceLoadOk);
        public void IncrementDataSourceLoadError() => Interlocked.Increment(ref _dataSourceLoadError);
        public void IncrementDataSourceKeyLoadHits(long value) => Interlocked.Add(ref _dataSourceKeyLoadHits, value);
        public void IncrementDataSourceKeyLoadMisses(long value) => Interlocked.Add(ref _dataSourceKeyLoadMisses, value);
        public void IncrementDataSourceKeyLoadErrors(long value) => Interlocked.Add(ref _dataSourceKeyLoadErrors, value);

        protected override void OnEventCommand(EventCommandEventArgs args)
        {
            if (args.Command != EventCommand.Enable)
            {
                return;
            }

            _localCacheGetHitsCounter ??= new IncrementingPollingCounter("local-cache-get-hits-rate", this,
                () => Volatile.Read(ref _localCacheGetHits))
            {
                DisplayName = "Local Cache Get Hits Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _localCacheGetMissesCounter ??= new IncrementingPollingCounter("local-cache-get-misses-rate", this,
                () => Volatile.Read(ref _localCacheGetMisses))
            {
                DisplayName = "Local Cache Get Misses Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _localCacheSetCounter ??= new IncrementingPollingCounter("local-cache-set-rate", this,
                () => Volatile.Read(ref _localCacheSet))
            {
                DisplayName = "Local Cache Set Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _localCacheRemoveCounter ??= new IncrementingPollingCounter("local-cache-remove-rate", this,
                () => Volatile.Read(ref _localCacheRemove))
            {
                DisplayName = "Local Cache Remove Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _distributedCacheGetHitsCounter ??= new IncrementingPollingCounter("distributed-cache-get-hits-rate", this,
                () => Volatile.Read(ref _distributedCacheGetHits))
            {
                DisplayName = "Distributed Cache Get Hits Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _distributedCacheGetMissesCounter ??= new IncrementingPollingCounter("distributed-cache-get-misses-rate",
                this,
                () => Volatile.Read(ref _distributedCacheGetMisses))
            {
                DisplayName = "Distributed Cache Get Misses Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _distributedCacheGetErrorsCounter ??= new IncrementingPollingCounter("distributed-cache-get-errors-rate",
                this,
                () => Volatile.Read(ref _distributedCacheGetErrors))
            {
                DisplayName = "Distributed Cache Get Errors Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _distributedCacheSetCounter ??= new IncrementingPollingCounter("distributed-cache-set-rate", this,
                () => Volatile.Read(ref _distributedCacheSet))
            {
                DisplayName = "Distributed Cache Set Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _distributedCacheRemoveCounter ??= new IncrementingPollingCounter("distributed-cache-remove-rate", this,
                () => Volatile.Read(ref _distributedCacheRemove))
            {
                DisplayName = "Distributed Cache Remove Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _dataSourceLoadOkCounter ??= new IncrementingPollingCounter("data-source-load-ok-rate", this,
                () => Volatile.Read(ref _dataSourceLoadOk))
            {
                DisplayName = "Data Source Successful Load Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _dataSourceLoadErrorCounter ??= new IncrementingPollingCounter("data-source-load-error-rate", this,
                () => Volatile.Read(ref _dataSourceLoadError))
            {
                DisplayName = "Data Source Failed Load Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _dataSourceKeyLoadHitsCounter ??= new IncrementingPollingCounter("data-source-key-load-hits-rate", this,
                () => Volatile.Read(ref _dataSourceKeyLoadHits))
            {
                DisplayName = "Data Source Key Load Hits Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _dataSourceKeyLoadMissesCounter ??= new IncrementingPollingCounter("data-source-key-load-misses-rate", this,
                () => Volatile.Read(ref _dataSourceKeyLoadMisses))
            {
                DisplayName = "Data Source Key Load Misses Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            _dataSourceKeyLoadErrorsCounter ??= new IncrementingPollingCounter("data-source-key-load-errors-rate", this,
                () => Volatile.Read(ref _dataSourceKeyLoadErrors))
            {
                DisplayName = "Data Source Key Load Errors Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };

            var counters = new[]
            {
                _localCacheGetHitsCounter,
                _localCacheGetMissesCounter,
                _localCacheSetCounter,
                _localCacheRemoveCounter,
                _distributedCacheGetHitsCounter,
                _distributedCacheGetMissesCounter,
                _distributedCacheGetErrorsCounter,
                _distributedCacheSetCounter,
                _distributedCacheRemoveCounter,
                _dataSourceLoadOkCounter,
                _dataSourceLoadErrorCounter,
                _dataSourceKeyLoadHitsCounter,
                _dataSourceKeyLoadMissesCounter,
                _dataSourceKeyLoadErrorsCounter,
            };
            foreach (var counter in counters)
            {
                counter.AddMetadata("cache-name", _cacheName);
            }
        }
    }
}
