using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public abstract class ConnectionStringContext : DbContext, ICommandCreator
    {
        public string ConnectionString { get; }

        protected internal ConnectionStringContext(ContextProvider contextProvider, string connectionString,
            QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
            : base(contextProvider, queryOptions, bulkOptions, readOptions)
        {
            this.ConnectionString = connectionString;
        }

        protected internal abstract DbConnection CreateConnection();

        public void UsingConnection(Action<ConnectionContext> action)
        {
            using (var conn = CreateConnection())
            {
                conn.Open();
                action(ContextProvider.GetConnectionContext(conn, QueryOptions, BulkOptions, ReadOptions));
            }
        }

        public async Task UsingConnectionAsync(Func<ConnectionContext, Task> action)
        {
            using (var conn = CreateConnection())
            {
                await conn.OpenAsync().ConfigureAwait(false);
                await action(ContextProvider.GetConnectionContext(conn, QueryOptions, BulkOptions, ReadOptions)).ConfigureAwait(false);
            }
        }

        public void UsingTransaction(IsolationLevel isolationLevel, Action<TransactionContext> action)
        {
            UsingConnection(connectionContext => connectionContext.UsingTransaction(isolationLevel, action));
        }

        public void UsingTransaction(Action<TransactionContext> action)
        {
            UsingTransaction(IsolationLevel.Unspecified, action);
        }

        public Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action)
        {
            return UsingConnectionAsync(connectionContext => connectionContext.UsingTransactionAsync(isolationLevel, action));
        }

        public Task UsingTransactionAsync(Func<TransactionContext, Task> action)
        {
            return UsingTransactionAsync(IsolationLevel.Unspecified, action);
        }

        private WCBase GetWC()
        {
            return WCBase.Create(this);
        }

        #region ICommandCreator

        public IWrappedCommand CreateSimple(QueryOptions queryOptions, SimpleCommand precompiledCommand, params object[] parameters)
        {
            return GetWC().Simple(queryOptions, precompiledCommand, parameters);
        }

        public IWrappedCommand CreateSimple(QueryOptions queryOptions, string commandText, params object[] parameters)
        {
            return GetWC().Simple(queryOptions, commandText, parameters);
        }

        public IWrappedCommand CreateProcedureSimple(QueryOptions queryOptions, string name, params DbParameter[] parameters)
        {
            return GetWC().ProcedureSimple(queryOptions, name, parameters);
        }


        public IWrappedCommand CreateMapped<T>(MappedCommand<T> precompiledCommand, T value, QueryOptions queryOptions = null)
        {
            return GetWC().Mapped(precompiledCommand, value, queryOptions);
        }

        public IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return GetWC().Mapped(commandText, paramNames, settings, value, queryOptions);
        }

        public IWrappedCommand CreateMapped<T>(string commandText, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return GetWC().Mapped(commandText, settings, value, queryOptions);
        }

        public IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, T value, FromTypeOption fromTypeOption = FromTypeOption.Default, QueryOptions queryOptions = null)
        {
            return GetWC().Mapped(commandText, paramNames, value, fromTypeOption, queryOptions);
        }

        public IWrappedCommand CreateMapped<T>(string commandText, T value, FromTypeOption fromTypeOption = FromTypeOption.Default, QueryOptions queryOptions = null)
        {
            return GetWC().Mapped(commandText, value, fromTypeOption, queryOptions);
        }

        public IWrappedCommand CreateProcedure<T>(string name, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return GetWC().Procedure(name, paramNames, settings, value, queryOptions);
        }

        public IWrappedCommand CreateProcedure<T>(string name, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return GetWC().Procedure(name, settings, value, queryOptions);
        }

        public IWrappedCommand CreateProcedure<T>(string name, IList<string> paramNames, T value, FromTypeOption fromTypeOption = FromTypeOption.Both, QueryOptions queryOptions = null)
        {
            return GetWC().Procedure(name, paramNames, value, fromTypeOption, queryOptions);
        }

        public IWrappedCommand CreateProcedure<T>(string name, T value, FromTypeOption fromTypeOption = FromTypeOption.Both, QueryOptions queryOptions = null)
        {
            return GetWC().Procedure(name, value, fromTypeOption, queryOptions);
        }

        public IWrappedCommand CreateInsert<T>(string tableName, T value, QueryOptions queryOptions, bool getIdentity, params string[] ignoreFields)
        {
            return GetWC().Insert(tableName, value, queryOptions, getIdentity, ignoreFields);
        }

        public IWrappedCommand CreateUpdate<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return GetWC().Update(tableName, value, queryOptions, keyFields);
        }

        public IWrappedCommand CreateDelete<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return GetWC().Delete(tableName, value, queryOptions, keyFields);
        }

        public IWrappedCommand CreateMerge<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields)
        {
            return GetWC().Merge(tableName, value, queryOptions, keyFields);
        }

        #endregion
    }
}
