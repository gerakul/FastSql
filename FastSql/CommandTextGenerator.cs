using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
    public static class CommandTextGenerator
    {
        public static string ColumnList(params string[] fields)
        {
            return string.Join(", ", fields.Select(x => $"[{x}]"));
        }
        public static string ParamList(params string[] fields)
        {
            return string.Join(", ", fields.Select(x => $"@{x}"));
        }

        public static string WhereClause(params string[] fields)
        {
            return string.Join(" and ", fields.Select(x => $"[{x}] = @{x}"));
        }

        public static string SetClause(params string[] fields)
        {
            return string.Join(", ", fields.Select(x => $"[{x}] = @{x}"));
        }

        public static string Insert(string tableName, bool getIdentity, params string[] fields)
        {
            var query = $"insert into {tableName} ({ColumnList(fields)}) values ({ParamList(fields)});";
            if (getIdentity)
            {
                query += " select scope_identity();";
            }

            return query;
        }

        public static string Update(string tableName, string whereClause, params string[] fields)
        {
            return $"update {tableName} set {SetClause(fields)} where {whereClause};";
        }

        public static string Update(string tableName, IEnumerable<string> keyFields, params string[] fields)
        {
            return Update(tableName, WhereClause(keyFields.ToArray()), fields);
        }

        public static string Merge(string tableName, IEnumerable<string> keyFields, params string[] fields)
        {
            var allFields = keyFields.Union(fields).ToArray();

            var usingList = ParamList(allFields);
            var sourceList = ColumnList(allFields);
            var onClause = string.Join(" and ", keyFields.Select(x => $"target.[{x}] = source.[{x}]"));
            var setClause = string.Join(", ", fields.Select(x => $"[{x}] = source.[{x}]"));
            var valuesClause = string.Join(", ", allFields.Select(x => $"source.[{x}]"));

            var cmd = $"merge {tableName} as target"
                + Environment.NewLine + $"using (select {usingList})"
                + Environment.NewLine + $"as source ({sourceList})"
                + Environment.NewLine + $"on ({onClause})"
                + Environment.NewLine + $"when matched then"
                + Environment.NewLine + $"update set {setClause}"
                + Environment.NewLine + $"when not matched then"
                + Environment.NewLine + $"insert ({sourceList})"
                + Environment.NewLine + $"values ({valuesClause});";

            return cmd;
        }

        public static string CreateTableScript(this IEnumerable<ColumnDefinition> columnDefinitions, string tableName, bool checkIfNotExists = false)
        {
            string create = $@"
create table {tableName}
(
{"\t"} {string.Join(Environment.NewLine + "\t,", columnDefinitions.Where(x => x != null).Select(x => x.GetColumnDeclaration()))}
)
";

            if (checkIfNotExists)
            {
                return $@"
if object_id (N'{tableName}', N'U') is null
begin
{create}
end
";
            }
            else
            {
                return create;
            }

        }

        public static string CreateTableScript<T>(string tableName, bool checkIfNotExists = false,
            ColumnDefinitionOptions options = null, FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            return FieldSettings.FromType<T>(fromTypeOption).GetColumnDefinitions(options).CreateTableScript(tableName, checkIfNotExists);
        }

        public static string CreateTableScript<T>(T proto, string tableName, bool checkIfNotExists = false,
            ColumnDefinitionOptions options = null, FromTypeOption fromTypeOption = FromTypeOption.Both)
        {
            return CreateTableScript<T>(tableName, checkIfNotExists, options, fromTypeOption);
        }
    }
}
