using System;
using System.Diagnostics.Tracing;
using System.Threading;

namespace ModernCaching.Instrumentation
{
    [EventSource(Name = "ModernCaching")]
    internal class EventCounterCacheMetrics : EventSource, ICacheMetrics
    {
        private readonly string _cacheName;

        // ReSharper disable NotAccessedField.Local
        private IncrementingPollingCounter? _localCacheGetHitsCounter;
        private IncrementingPollingCounter? _localCacheGetMissesCounter;
        private IncrementingPollingCounter? _localCacheSetCounter;
        private IncrementingPollingCounter? _localCacheDeleteCounter;
        private PollingCounter? _localCacheCountCounter;
        private IncrementingPollingCounter? _distributedCacheGetHitsCounter;
        private IncrementingPollingCounter? _distributedCacheGetMissesCounter;
        private IncrementingPollingCounter? _distributedCacheGetErrorsCounter;
        private IncrementingPollingCounter? _distributedCacheSetCounter;
        private IncrementingPollingCounter? _distributedCacheDeleteCounter;
        private IncrementingPollingCounter? _dataSourceLoadOkCounter;
        private IncrementingPollingCounter? _dataSourceLoadErrorCounter;
        private IncrementingPollingCounter? _dataSourceKeyLoadHitsCounter;
        private IncrementingPollingCounter? _dataSourceKeyLoadMissesCounter;
        private IncrementingPollingCounter? _dataSourceKeyLoadErrorsCounter;
        // ReSharper restore NotAccessedField.Local

        private long _localCacheGetHits;
        private long _localCacheGetMisses;
        private long _localCacheSet;
        private long _localCacheDelete;
        private Func<double>? _localCacheCountPoller;
        private long _distributedCacheGetHits;
        private long _distributedCacheGetMisses;
        private long _distributedCacheGetErrors;
        private long _distributedCacheSet;
        private long _distributedCacheDelete;
        private long _dataSourceLoadOk;
        private long _dataSourceLoadError;
        private long _dataSourceKeyLoadHits;
        private long _dataSourceKeyLoadMisses;
        private long _dataSourceKeyLoadErrors;

        public EventCounterCacheMetrics(string cacheName) => _cacheName = cacheName;

        public void IncrementLocalCacheGetHits() => Interlocked.Increment(ref _localCacheGetHits);
        public void IncrementLocalCacheGetMisses() => Interlocked.Increment(ref _localCacheGetMisses);
        public void IncrementLocalCacheSet() => Interlocked.Increment(ref _localCacheSet);
        public void IncrementLocalCacheDelete() => Interlocked.Increment(ref _localCacheDelete);
        // Ugly. Is there a better way?
        public void SetLocalCacheCountPoller(Func<double> poller) => _localCacheCountPoller = poller;
        public void IncrementDistributedCacheGetHits() => Interlocked.Increment(ref _distributedCacheGetHits);
        public void IncrementDistributedCacheGetMisses() => Interlocked.Increment(ref _distributedCacheGetMisses);
        public void IncrementDistributedCacheGetErrors() => Interlocked.Increment(ref _distributedCacheGetErrors);
        public void IncrementDistributedCacheSet() => Interlocked.Increment(ref _distributedCacheSet);
        public void IncrementDistributedCacheDelete() => Interlocked.Increment(ref _distributedCacheDelete);
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

            IncrementingPollingCounter CreateLocalCacheCounter(string operation, string? status,
                Func<double> totalValueProvider)
            {
                var counter = CreateCounter("local-cache-requests-rate", "Local Cache Requests Rate",
                    totalValueProvider);
                counter.AddMetadata("operation", operation);
                if (status != null)
                {
                    counter.AddMetadata("status", status);
                }

                return counter;
            }

            _localCacheGetHitsCounter ??= CreateLocalCacheCounter("get", "hit",
                () => Volatile.Read(ref _localCacheGetHits));
            _localCacheGetMissesCounter ??= CreateLocalCacheCounter("get", "miss",
                () => Volatile.Read(ref _localCacheGetMisses));
            _localCacheSetCounter ??= CreateLocalCacheCounter("set", null,
                () => Volatile.Read(ref _localCacheSet));
            _localCacheDeleteCounter ??= CreateLocalCacheCounter("del", null,
                () => Volatile.Read(ref _localCacheDelete));

            if (_localCacheCountCounter != null)
            {
                _localCacheCountCounter ??= new PollingCounter("local-cache-count", this, _localCacheCountPoller)
                {
                    DisplayName = "Local Cache Count",
                };
            }

            IncrementingPollingCounter CreateDistributedCacheCounter(string operation, string? status,
                Func<double> totalValueProvider)
            {
                var counter = CreateCounter("distributed-cache-requests-rate", "Distributed Cache Requests Rate",
                    totalValueProvider);
                counter.AddMetadata("operation", operation);
                if (status != null)
                {
                    counter.AddMetadata("status", status);
                }

                return counter;
            }

            _distributedCacheGetHitsCounter ??= CreateDistributedCacheCounter("get", "hit",
                () => Volatile.Read(ref _distributedCacheGetHits));
            _distributedCacheGetMissesCounter ??= CreateDistributedCacheCounter("get", "miss",
                () => Volatile.Read(ref _distributedCacheGetMisses));
            _distributedCacheGetErrorsCounter ??= CreateDistributedCacheCounter("get", "error",
                () => Volatile.Read(ref _distributedCacheGetErrors));
            _distributedCacheSetCounter ??= CreateDistributedCacheCounter("set", null,
                () => Volatile.Read(ref _distributedCacheSet));
            _distributedCacheDeleteCounter ??= CreateDistributedCacheCounter("del", null,
                () => Volatile.Read(ref _distributedCacheDelete));

            IncrementingPollingCounter CreateLoadCounter(string status, Func<double> totalValueProvider)
            {
                var counter = CreateCounter("data-source-load-rate", "Data Source Load Rate", totalValueProvider);
                counter.AddMetadata("status", status);
                return counter;
            }

            _dataSourceLoadOkCounter ??= CreateLoadCounter("ok", () => Volatile.Read(ref _dataSourceLoadOk));
            _dataSourceLoadErrorCounter ??= CreateLoadCounter("error", () => Volatile.Read(ref _dataSourceLoadError));

            IncrementingPollingCounter CreateKeyLoadCounter(string status, Func<double> totalValueProvider)
            {
                var counter = CreateCounter("data-source-key-load-rate", "Data Source Key Load Rate",
                    totalValueProvider);
                counter.AddMetadata("status", status);
                return counter;
            }

            _dataSourceKeyLoadHitsCounter ??= CreateKeyLoadCounter("hit",
                () => Volatile.Read(ref _dataSourceKeyLoadHits));
            _dataSourceKeyLoadMissesCounter ??= CreateKeyLoadCounter("miss",
                () => Volatile.Read(ref _dataSourceKeyLoadMisses));
            _dataSourceKeyLoadErrorsCounter ??= CreateKeyLoadCounter("error",
                () => Volatile.Read(ref _dataSourceKeyLoadErrors));
        }

        private IncrementingPollingCounter CreateCounter(string name, string displayName, Func<double> totalValueProvider)
        {
            IncrementingPollingCounter counter = new(name, this, totalValueProvider)
            {
                DisplayName = displayName,
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };
            counter.AddMetadata("name", _cacheName);
            return counter;
        }
    }
}
