using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ModernCaching.DataSource;
using ModernCaching.LocalCaching;
using NUnit.Framework;

namespace ModernCaching.ITest;

/// <summary>
/// - local cache: builtin MemoryCache
/// - distributed cache: none
/// - data source: Web API (ip-api.com)
/// </summary>
public class WebApiCache
{
    private IReadOnlyCache<IPAddress, LatLon> _cache = default!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cache = await new ReadOnlyCacheBuilder<IPAddress, LatLon>(new ReadOnlyCacheOptions("ip-lat-lng", TimeSpan.FromDays(1)))
            .WithLocalCache(new MemoryCache<IPAddress, LatLon>())
            .WithDataSource(new IpApiDataSource())
            .WithLoggerFactory(new ConsoleLoggerFactory())
            .BuildAsync();
    }

    [Test, Explicit]
    public async Task TestGoogle()
    {
        var (found, _) = await _cache.TryGetAsync(IPAddress.Parse("8.8.8.8"));
        Assert.That(found, Is.True);
    }

    private class IpApiDataSource : IDataSource<IPAddress, LatLon>
    {
        private readonly HttpClient _httpClient;

        public IpApiDataSource()
        {
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri("http://ip-api.com/json/"),
            };
        }

#pragma warning disable 1998
        public async IAsyncEnumerable<KeyValuePair<IPAddress, LatLon>> LoadAsync(
#pragma warning restore 1998
            IEnumerable<IPAddress> ips,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var ip in ips)
            {
                var latLon = await _httpClient.GetFromJsonAsync<LatLon>($"{ip}?fields=message,lat,lon",
                    cancellationToken: cancellationToken);
                yield return new KeyValuePair<IPAddress, LatLon>(ip, latLon!);
            }
        }
    }

    private record LatLon(float Lat, float Lon);
}
