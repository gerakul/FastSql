using System;
using System.Collections.Generic;
using System.Linq;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlCommandTextGenerator : CommandTextGenerator
    {
        public override string ColumnList(params string[] fields)
        {
            return string.Join(", ", fields.Select(x => $"\"{x}\""));
        }

        public override string ParamList(params string[] fields)
        {
            return string.Join(", ", fields.Select(x => $"@{x}"));
        }

        public override string WhereClause(params string[] fields)
        {
            return string.Join(" and ", fields.Select(x => $"\"{x}\" = @{x}"));
        }

        public override string SetClause(params string[] fields)
        {
            return string.Join(", ", fields.Select(x => $"\"{x}\" = @{x}"));
        }

        public override string Insert(string tableName, params string[] fields)
        {
            return $"insert into {tableName} ({ColumnList(fields)}) values ({ParamList(fields)});";
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
            var setClause = string.Join(", ", fields.Select(x => $"\"{x}\" = excluded.\"{x}\""));

            var cmd = $"insert into \"{tableName}\" ({ColumnList(allFields)}) "
                + Environment.NewLine + $"values ({ParamList(allFields)}) "
                + Environment.NewLine + $"on conflict ({ColumnList(keyFields.ToArray())}) "
                + Environment.NewLine + $"do update set {setClause};";

            return cmd;
        }
    }
}
