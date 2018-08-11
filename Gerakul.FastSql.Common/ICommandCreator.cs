using System.Collections.Generic;

namespace Gerakul.FastSql.Common
{
    public interface ICommandCreator
    {
        IWrappedCommand CreateSimple(QueryOptions queryOptions, SimpleCommand precompiledCommand, params object[] parameters);
        IWrappedCommand CreateSimple(QueryOptions queryOptions, string commandText, params object[] parameters);

        IWrappedCommand CreateMapped<T>(MappedCommand<T> precompiledCommand, T value, QueryOptions queryOptions = null);
        IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null);
        IWrappedCommand CreateMapped<T>(string commandText, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null);
        IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, T value, FromTypeOption fromTypeOption = FromTypeOption.Default, QueryOptions queryOptions = null);
        IWrappedCommand CreateMapped<T>(string commandText, T value, FromTypeOption fromTypeOption = FromTypeOption.Default, QueryOptions queryOptions = null);

        IWrappedCommand CreateProcedure<T>(string name, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null);
        IWrappedCommand CreateProcedure<T>(string name, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null);
        IWrappedCommand CreateProcedure<T>(string name, IList<string> paramNames, T value, FromTypeOption fromTypeOption = FromTypeOption.Default, QueryOptions queryOptions = null);
        IWrappedCommand CreateProcedure<T>(string name, T value, FromTypeOption fromTypeOption = FromTypeOption.Default, QueryOptions queryOptions = null);

        IWrappedCommand CreateInsert<T>(string tableName, T value, QueryOptions queryOptions, bool getIdentity, params string[] ignoreFields);
        IWrappedCommand CreateUpdate<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields);
        IWrappedCommand CreateDelete<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields);
        IWrappedCommand CreateMerge<T>(string tableName, T value, QueryOptions queryOptions, params string[] keyFields);
    }

    public static class CommandCreatorExtensions
    {
        public static IWrappedCommand CreateSimple(this ICommandCreator creator, SimpleCommand precompiledCommand, params object[] parameters)
        {
            return creator.CreateSimple(null, precompiledCommand, parameters);
        }

        public static IWrappedCommand CreateSimple(this ICommandCreator creator, string commandText, params object[] parameters)
        {
            return creator.CreateSimple(null, commandText, parameters);
        }

        public static IWrappedCommand CreateInsert<T>(this ICommandCreator creator, string tableName, T value, bool getIdentity, params string[] ignoreFields)
        {
            return creator.CreateInsert(tableName, value, null, getIdentity, ignoreFields);
        }

        public static IWrappedCommand CreateInsert<T>(this ICommandCreator creator, string tableName, T value, params string[] ignoreFields)
        {
            return creator.CreateInsert(tableName, value, null, false, ignoreFields);
        }

        public static IWrappedCommand CreateUpdate<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
        {
            return creator.CreateUpdate(tableName, value, null, keyFields);
        }

        public static IWrappedCommand CreateDelete<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
        {
            return creator.CreateDelete(tableName, value, null, keyFields);
        }

        public static IWrappedCommand CreateMerge<T>(this ICommandCreator creator, string tableName, T value, params string[] keyFields)
        {
            return creator.CreateMerge(tableName, value, null, keyFields);
        }
    }
}
