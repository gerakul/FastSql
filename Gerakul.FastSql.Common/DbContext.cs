using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public abstract class DbContext : ICommandCreator
    {
        protected internal abstract CommandTextGenerator CommandTextGenerator { get; }
        public abstract QueryOptions DefaultQueryOptions { get; }

        public ExecutionOptions DefaultExecutionOptions { get; }
        public string ConnectionString { get; }
        public CommandCompilator CommandCompilator { get; }

        public DbContext(string connectionString)
        {
            this.DefaultExecutionOptions = new ExecutionOptions(DefaultQueryOptions, new ReadOptions());
            this.ConnectionString = connectionString;
            this.CommandCompilator = new CommandCompilator(this);
        }

        protected internal virtual void ApplyQueryOptions(DbCommand cmd, QueryOptions queryOptions)
        {
            if (queryOptions == null)
            {
                return;
            }

            if (queryOptions.CommandTimeoutSeconds.HasValue)
            {
                cmd.CommandTimeout = queryOptions.CommandTimeoutSeconds.Value;
            }
        }

        protected internal abstract DbParameter AddParamWithValue(DbCommand cmd, string paramName, object value);

        protected internal abstract string GetSqlParameterName(string name);

        // :::
        //protected internal abstract BulkOptions GetDafaultBulkOptions();

        //protected internal abstract BulkCopy GetBulkCopy(DbConnection connection, DbTransaction transaction, BulkOptions bulkOptions, string destinationTable, DbDataReader reader, params string[] fields);

        protected internal abstract DbConnection GetConnection();

        protected internal abstract object GetDbNull(Type type);

        protected internal abstract string[] ParseCommandText(string commandText);

        public void UsingConnection(Action<ConnectionScope> action)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                action(new ConnectionScope(this, conn));
            }
        }

        public async Task UsingConnectionAsync(Func<ConnectionScope, Task> action)
        {
            using (var conn = GetConnection())
            {
                await conn.OpenAsync().ConfigureAwait(false);
                await action(new ConnectionScope(this, conn)).ConfigureAwait(false);
            }
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
