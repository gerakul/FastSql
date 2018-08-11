using System.Collections.Generic;
using System.Linq;

namespace Gerakul.FastSql
{
    public abstract class CommandTextGenerator
    {
        public abstract string ColumnList(params string[] fields);

        public abstract string ParamList(params string[] fields);

        public abstract string WhereClause(params string[] fields);

        public abstract string SetClause(params string[] fields);

        public abstract string Insert(string tableName, bool getIdentity, params string[] fields);

        public abstract string Update(string tableName, string whereClause, params string[] fields);

        public string Update(string tableName, IEnumerable<string> keyFields, params string[] fields)
        {
            return Update(tableName, WhereClause(keyFields.ToArray()), fields);
        }

        public abstract string Merge(string tableName, IEnumerable<string> keyFields, params string[] fields);
    }
}
