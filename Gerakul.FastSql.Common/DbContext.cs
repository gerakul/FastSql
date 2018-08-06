using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public abstract class DbContext : ContextBase, ICommandCreator
    {
        public string ConnectionString { get; }

        public DbContext(ContextProvider contextProvider, string connectionString)
            : base(contextProvider)
        {
            this.ConnectionString = connectionString;
        }

        protected internal abstract DbConnection CreateConnection();

        public void UsingConnection(Action<ConnectionContext> action)
        {
            using (var conn = CreateConnection())
            {
                conn.Open();
                action(ContextProvider.CreateConnectionContext(conn));
            }
        }

        public async Task UsingConnectionAsync(Func<ConnectionContext, Task> action)
        {
            using (var conn = CreateConnection())
            {
                await conn.OpenAsync().ConfigureAwait(false);
                await action(ContextProvider.CreateConnectionContext(conn)).ConfigureAwait(false);
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

        public IWrappedCommand CreateSimple(SimpleCommand precompiledCommand, params object[] parameters)
        {
            return GetWC().Simple(precompiledCommand, parameters);
        }

        public IWrappedCommand CreateSimple(QueryOptions queryOptions, string commandText, params object[] parameters)
        {
            return GetWC().Simple(queryOptions, commandText, parameters);
        }

        public IWrappedCommand CreateSimple(string commandText, params object[] parameters)
        {
            return GetWC().Simple(commandText, parameters);
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


        public IWrappedCommand CreateInsert<T>(QueryOptions queryOptions, string tableName, T value, bool getIdentity, params string[] ignoreFields)
        {
            return GetWC().Insert(queryOptions, tableName, value, getIdentity, ignoreFields);
        }

        public IWrappedCommand CreateInsert<T>(string tableName, T value, bool getIdentity, params string[] ignoreFields)
        {
            return GetWC().Insert(tableName, value, getIdentity, ignoreFields);
        }


        public IWrappedCommand CreateUpdate<T>(QueryOptions queryOptions, string tableName, T value, params string[] keyFields)
        {
            return GetWC().Update(queryOptions, tableName, value, keyFields);
        }

        public IWrappedCommand CreateUpdate<T>(string tableName, T value, params string[] keyFields)
        {
            return GetWC().Update(tableName, value, keyFields);
        }


        public IWrappedCommand CreateMerge<T>(QueryOptions queryOptions, string tableName, T value, params string[] keyFields)
        {
            return GetWC().Merge(queryOptions, tableName, value, keyFields);
        }

        public IWrappedCommand CreateMerge<T>(string tableName, T value, params string[] keyFields)
        {
            return GetWC().Merge(tableName, value, keyFields);
        }

        #endregion
    }
}
