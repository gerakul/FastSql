using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
    public static class IDbScopeExtensions
    {
        #region SimpleCommand

        internal static DbCommand CreateSimple(this IDbScope scope, QueryOptions queryOptions, SimpleCommand precompiledCommand, params object[] parameters)
        {
            return precompiledCommand.Create(scope, queryOptions, parameters);
        }

        internal static DbCommand CreateSimple(this IDbScope scope, SimpleCommand precompiledCommand, params object[] parameters)
        {
            return precompiledCommand.Create(scope, null, parameters);
        }

        public static DbCommand CreateSimple(this IDbScope scope, QueryOptions queryOptions, string commandText, params object[] parameters)
        {
            return SimpleCommand.Compile(commandText).Create(scope, queryOptions, parameters);
        }

        public static DbCommand CreateSimple(this IDbScope scope, string commandText, params object[] parameters)
        {
            return SimpleCommand.Compile(commandText).Create(scope, null, parameters);
        }

        #endregion

        #region MappedCommand

        public static DbCommand CreateMapped<T>(this IDbScope scope, MappedCommand<T> precompiledCommand, T value, QueryOptions queryOptions = null)
        {
            return precompiledCommand.Create(scope, value, queryOptions);
        }

        public static DbCommand CreateMapped<T>(this IDbScope scope, string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return MappedCommand.Compile(commandText, paramNames, settings).Create(scope, value, queryOptions);
        }

        public static DbCommand CreateMapped<T>(this IDbScope scope, string commandText, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null)
        {
            return MappedCommand.Compile(commandText, settings).Create(scope, value, queryOptions);
        }

        public static DbCommand CreateMapped<T>(this IDbScope scope, string commandText, IList<string> paramNames, T value, FromTypeOption fromTypeOption = FromTypeOption.Both, QueryOptions queryOptions = null)
        {
            return MappedCommand.Compile<T>(commandText, paramNames, fromTypeOption).Create(scope, value, queryOptions);
        }

        public static DbCommand CreateMapped<T>(this IDbScope scope, string commandText, T value, FromTypeOption fromTypeOption = FromTypeOption.Both, QueryOptions queryOptions = null)
        {
            return MappedCommand.Compile<T>(commandText, fromTypeOption).Create(scope, value, queryOptions);
        }

        #region Special commands


        #region Insert

        public static DbCommand CreateInsert<T>(this IDbScope scope, QueryOptions queryOptions, string tableName, T value, bool getIdentity, params string[] ignoreFields)
        {
            return MappedCommand.CompileInsert<T>(tableName, getIdentity, ignoreFields).Create(scope, value, queryOptions);
        }

        public static DbCommand CreateInsert<T>(this IDbScope scope, string tableName, T value, bool getIdentity, params string[] ignoreFields)
        {
            return MappedCommand.CompileInsert<T>(tableName, getIdentity, ignoreFields).Create(scope, value);
        }

        #endregion

        #region Update

        public static DbCommand CreateUpdate<T>(this IDbScope scope, QueryOptions queryOptions, string tableName, T value, params string[] keyFields)
        {
            return MappedCommand.CompileUpdate<T>(tableName, keyFields).Create(scope, value, queryOptions);
        }

        public static DbCommand CreateUpdate<T>(this IDbScope scope, string tableName, T value, params string[] keyFields)
        {
            return MappedCommand.CompileUpdate<T>(tableName, keyFields).Create(scope, value);
        }

        #endregion

        #region Merge

        public static DbCommand CreateMerge<T>(this IDbScope scope, QueryOptions queryOptions, string tableName, T value, params string[] keyFields)
        {
            return MappedCommand.CompileMerge<T>(tableName, keyFields).Create(scope, value, queryOptions);
        }

        public static DbCommand CreateMerge<T>(this IDbScope scope, string tableName, T value, params string[] keyFields)
        {
            return MappedCommand.CompileMerge<T>(tableName, keyFields).Create(scope, value);
        }

        #endregion


        #endregion

        #endregion
    }
}
