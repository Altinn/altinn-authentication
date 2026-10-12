#nullable enable
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.Telemetry;

namespace Altinn.Platform.Authentication.Tests.Fakes
{
    /// <summary>
    /// Stand-in for the <see cref="IMetricsProvider"/> that Altinn.Authorization.ServiceDefaults
    /// registers, so a plain unit test can instantiate a service that records metrics without
    /// building the whole host. Meters come from a real <see cref="IMeterFactory"/>, which lets a
    /// test observe the instruments with <c>MetricCollector</c>.
    /// </summary>
    public sealed class TestMetricsProvider(IMeterFactory meterFactory) : IMetricsProvider
    {
        private readonly ConcurrentDictionary<Type, object> _instances = new();
        private readonly IMeterFactory _meterFactory = meterFactory;

        /// <inheritdoc/>
        public T Get<T>()
            where T : IMetrics<T>
            => (T)_instances.GetOrAdd(
                typeof(T),
                _ => T.Create(_meterFactory.Create(new MeterOptions(T.MeterName) { Version = T.MeterVersion })));
    }
}
