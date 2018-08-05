using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public interface IWrappedCommand
    {
        int ExecuteNonQuery();
        Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default(CancellationToken));
        object ExecuteScalar();
        Task<object> ExecuteScalarAsync(CancellationToken cancellationToken = default(CancellationToken));

        IEnumerable<object[]> ExecuteQuery();
        IAsyncEnumerable<object[]> ExecuteQueryAsync(CancellationToken cancellationToken = default(CancellationToken));
        IEnumerable<T> ExecuteQuery<T>(ReadOptions readOptions = null);
        IAsyncEnumerable<T> ExecuteQueryAsync<T>(ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken)) where T : new();
        IEnumerable<T> ExecuteQueryAnonymous<T>(T proto, ReadOptions readOptions = null);
        IAsyncEnumerable<T> ExecuteQueryAnonymousAsync<T>(T proto, ReadOptions readOptions = null, CancellationToken cancellationToken = default(CancellationToken));
        IEnumerable ExecuteQueryFirstColumn();
        IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(CancellationToken cancellationToken = default(CancellationToken));
        IEnumerable<T> ExecuteQueryFirstColumn<T>();
        IAsyncEnumerable<T> ExecuteQueryFirstColumnAsync<T>(CancellationToken cancellationToken = default(CancellationToken));
    }
}
