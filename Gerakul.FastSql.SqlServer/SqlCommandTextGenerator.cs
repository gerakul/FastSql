using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql
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

        public override string Insert(string tableName, bool getIdentity, params string[] fields)
        {
            var query = $"insert into {tableName} ({ColumnList(fields)}) values ({ParamList(fields)});";
            if (getIdentity)
            {
                query += " select scope_identity();";
            }

            return query;
        }

        public override string Update(string tableName, string whereClause, params string[] fields)
        {
            return $"update {tableName} set {SetClause(fields)} where {whereClause};";
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
