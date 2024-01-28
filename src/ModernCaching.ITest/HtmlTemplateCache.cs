using System;
using System.Collections.Generic;
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
/// - data source: CPU task (HTML templating)
/// </summary>
public class HtmlTemplateCache
{
    private IReadOnlyCache<TemplateData, string> _cache = default!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _cache = await new ReadOnlyCacheBuilder<TemplateData, string>("templates")
            .WithLocalCache(new MemoryCache<TemplateData, string>())
            .WithDataSource(new TemplateDataSource())
            .WithLoggerFactory(new ConsoleLoggerFactory())
            .BuildAsync();
    }

    [Test]
    public async Task CacheEntriesShouldOnlyBeMatchedByUserId()
    {
        var (found, template1) = await _cache.TryGetAsync(new TemplateData(1, "toto"));
        Assert.That(found, Is.True);
        Assert.That(template1!.Length, Is.Not.Zero);
        Assert.That(_cache.TryPeek(new TemplateData(1, "zozo"), out string? template2), Is.True);
        Assert.That(template2, Is.SameAs(template1));
    }

    private class TemplateDataSource : IDataSource<TemplateData, string>
    {
#pragma warning disable 1998
        public async IAsyncEnumerable<DataSourceResult<TemplateData, string>> LoadAsync(
#pragma warning restore 1998
            IEnumerable<TemplateData> datas,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var data in datas)
            {
                yield return new DataSourceResult<TemplateData, string>(data, Render(data), TimeSpan.FromHours(1));
            }
        }

        private string Render(TemplateData data)
        {
            return $@"
<!doctype html>
<html>
  <head>
    <title>Hello World</title>
  </head>
  <body>
    <p>Hello {data.Name}!</p>
  </body>
</html>";
        }
    }

    public class TemplateData : IEquatable<TemplateData>
    {
        public TemplateData(int userId, string name)
        {
            UserId = userId;
            Name = name;
        }

        public int UserId { get; }
        public string Name { get; }

        // Don't include Name in the Equals methods so that the cache entry is only matched by UserId.
        public bool Equals(TemplateData? other) =>
            other != null
            && (ReferenceEquals(this, other) || UserId == other.UserId);

        public override bool Equals(object? obj) => Equals(obj as TemplateData);

        public override int GetHashCode() => UserId.GetHashCode();
    }
}
