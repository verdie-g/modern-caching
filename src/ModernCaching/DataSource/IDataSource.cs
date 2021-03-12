using System.Collections.Generic;
using System.Threading;

namespace ModernCaching.DataSource
{
    public interface IDataSource<TKey, TValue>
    {
        IAsyncEnumerable<LoadResult<TKey, TValue>> LoadAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken);
    }
}
