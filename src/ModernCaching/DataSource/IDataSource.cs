using System.Collections.Generic;
using System.Threading;

namespace ModernCaching.DataSource
{
    /// <summary>
    /// Source of truth of the data. Typically a stored procedure accessing a relational database.
    /// </summary>
    /// <typeparam name="TKey">Type of the identifier of the data.</typeparam>
    /// <typeparam name="TValue">Type of the data.</typeparam>
    public interface IDataSource<TKey, TValue>
    {
        /// <summary>
        /// Load the data associated with the given keys. If a key was not found in the source, the implementation should
        /// either not include a <see cref="DataSourceResult{TKey,TValue}"/> in the results for that key or include one with a
        /// null value so it will be cached in the distributed and local caches.
        /// </summary>
        /// <param name="keys">The keys to load.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.
        /// </param>
        /// <returns>
        /// The loading results associated with a duration during which the data is considered fresh. The <see cref="IAsyncEnumerable{T}"/>
        /// can contain less elements than <paramref name="keys"/> if some keys were not found.
        /// </returns>
        IAsyncEnumerable<DataSourceResult<TKey, TValue>> LoadAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken);
    }
}
