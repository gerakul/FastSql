using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
    public class PreparedMappedCommand<T>
    {
        private MappedCommand<T> mappedCommand;
        private string connectionString;
        private T value;

        internal PreparedMappedCommand(MappedCommand<T> mappedCommand, string connectionString, T value)
        {
            this.mappedCommand = mappedCommand;
            this.connectionString = connectionString;
            this.value = value;
        }


        #region Execution

        public int ExecuteNonQuery(QueryOptions queryOptions = null)
        {
            return mappedCommand.ExecuteNonQuery(connectionString, value, queryOptions);
        }

        public Task<int> ExecuteNonQueryAsync(QueryOptions queryOptions = null,
          CancellationToken cancellationToken = default(CancellationToken))
        {
            return mappedCommand.ExecuteNonQueryAsync(connectionString, value, queryOptions, cancellationToken);
        }

        public object ExecuteScalar(QueryOptions queryOptions = null)
        {
            return mappedCommand.ExecuteScalar(connectionString, value, queryOptions);
        }

        public Task<object> ExecuteScalarAsync(QueryOptions queryOptions = null,
          CancellationToken cancellationToken = default(CancellationToken))
        {
            return mappedCommand.ExecuteScalarAsync(connectionString, value, queryOptions, cancellationToken);
        }

        public IEnumerable<object[]> ExecuteQuery(ExecutionOptions options = null)
        {
            return mappedCommand.ExecuteQuery(connectionString, value, options);
        }

        public IAsyncEnumerable<object[]> ExecuteQueryAsync(ExecutionOptions options = null,
          CancellationToken cancellationToken = default(CancellationToken))
        {
            return mappedCommand.ExecuteQueryAsync(connectionString, value, options, cancellationToken);
        }

        public IEnumerable<R> ExecuteQuery<R>(ExecutionOptions options = null) where R : new()
        {
            return mappedCommand.ExecuteQuery<R>(connectionString, value, options);
        }

        public IAsyncEnumerable<R> ExecuteQueryAsync<R>(ExecutionOptions options = null,
          CancellationToken cancellationToken = default(CancellationToken)) where R : new()
        {
            return mappedCommand.ExecuteQueryAsync<R>(connectionString, value, options, cancellationToken);
        }

        public IEnumerable<R> ExecuteQueryAnonymous<R>(R proto, ExecutionOptions options = null)
        {
            return mappedCommand.ExecuteQueryAnonymous(proto, connectionString, value, options);
        }

        public IAsyncEnumerable<R> ExecuteQueryAnonymousAsync<R>(R proto, ExecutionOptions options = null,
          CancellationToken cancellationToken = default(CancellationToken))
        {
            return mappedCommand.ExecuteQueryAnonymousAsync(proto, connectionString, value, options, cancellationToken);
        }

        public IEnumerable ExecuteQueryFirstColumn(ExecutionOptions options = null)
        {
            return mappedCommand.ExecuteQueryFirstColumn(connectionString, value, options);
        }

        public IAsyncEnumerable<object> ExecuteQueryFirstColumnAsync(ExecutionOptions options = null,
          CancellationToken cancellationToken = default(CancellationToken))
        {
            return mappedCommand.ExecuteQueryFirstColumnAsync(connectionString, value, options, cancellationToken);
        }

        public IEnumerable<R> ExecuteQueryFirstColumn<R>(ExecutionOptions options = null)
        {
            return mappedCommand.ExecuteQueryFirstColumn<R>(connectionString, value, options);
        }

        public IAsyncEnumerable<R> ExecuteQueryFirstColumnAsync<R>(ExecutionOptions options = null,
          CancellationToken cancellationToken = default(CancellationToken))
        {
            return mappedCommand.ExecuteQueryFirstColumnAsync<R>(connectionString, value, options, cancellationToken);
        }

        #endregion
    }
}
