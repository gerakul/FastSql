using System;
using System.Collections.Generic;
using System.Text;

namespace Gerakul.FastSql.Common
{
    public interface ICommandCreator
    {
        IWrappedCommand CreateSimple(QueryOptions queryOptions, string commandText, params object[] parameters);
        IWrappedCommand CreateSimple(string commandText, params object[] parameters);


        IWrappedCommand CreateMapped<T>(MappedCommand<T> precompiledCommand, T value, QueryOptions queryOptions = null);
        IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null);
        IWrappedCommand CreateMapped<T>(string commandText, IList<FieldSettings<T>> settings, T value, QueryOptions queryOptions = null);
        IWrappedCommand CreateMapped<T>(string commandText, IList<string> paramNames, T value, FromTypeOption fromTypeOption = FromTypeOption.Default, QueryOptions queryOptions = null);
        IWrappedCommand CreateMapped<T>(string commandText, T value, FromTypeOption fromTypeOption = FromTypeOption.Default, QueryOptions queryOptions = null);

        IWrappedCommand CreateInsert<T>(QueryOptions queryOptions, string tableName, T value, bool getIdentity, params string[] ignoreFields);
        IWrappedCommand CreateInsert<T>(string tableName, T value, bool getIdentity, params string[] ignoreFields);

        IWrappedCommand CreateUpdate<T>(QueryOptions queryOptions, string tableName, T value, params string[] keyFields);
        IWrappedCommand CreateUpdate<T>(string tableName, T value, params string[] keyFields);

        IWrappedCommand CreateMerge<T>(QueryOptions queryOptions, string tableName, T value, params string[] keyFields);
        IWrappedCommand CreateMerge<T>(string tableName, T value, params string[] keyFields);
    }
}
