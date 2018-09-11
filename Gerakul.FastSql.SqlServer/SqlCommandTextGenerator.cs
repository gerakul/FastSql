using System;
using System.Collections.Generic;
using System.Linq;

namespace Gerakul.FastSql.SqlServer
{
    public class SqlCommandTextGenerator : CommandTextGenerator
    {
        public override string ColumnList(params string[] fields)
        {
            return string.Join(", ", fields.Select(x => $"[{x}]"));
        }

        public override string ParamList(params string[] fields)
        {
            return string.Join(", ", fields.Select(x => $"@{x}"));
        }

        public override string WhereClause(params string[] fields)
        {
            return string.Join(" and ", fields.Select(x => $"[{x}] = @{x}"));
        }

        public override string SetClause(params string[] fields)
        {
            return string.Join(", ", fields.Select(x => $"[{x}] = @{x}"));
        }

        public override string Insert(string tableName, params string[] fields)
        {
            return $"insert into {tableName} ({ColumnList(fields)}) values ({ParamList(fields)});";
        }

        public string InsertAndGetID(string tableName, params string[] fields)
        {
            return Insert(tableName, fields) + " select scope_identity();";
        }

        public override string Update(string tableName, string whereClause, params string[] fields)
        {
            return $"update {tableName} set {SetClause(fields)} where {whereClause};";
        }

        public override string Delete(string tableName, string whereClause)
        {
            return $"delete from {tableName} where {whereClause};";
        }

        public override string Merge(string tableName, IEnumerable<string> keyFields, params string[] fields)
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
    }
}
