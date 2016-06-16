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
  }
}
