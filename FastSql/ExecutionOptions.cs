using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
  public class ExecutionOptions
  {
    public QueryOptions QueryOptions { get; private set; }
    public ReadOptions ReadOptions { get; private set; }

    public ExecutionOptions(QueryOptions queryOptions, ReadOptions readOptions = null)
    {
      this.QueryOptions = queryOptions == null ? new QueryOptions() : queryOptions;
      this.ReadOptions = readOptions == null ? new ReadOptions() : readOptions;
    }

    public ExecutionOptions(int? commandTimeoutSeconds = null, FieldsSelector fieldsSelector = FieldsSelector.Destination, bool caseSensitive = false, FromTypeOption fromTypeOption = FromTypeOption.Both)
      : this(new QueryOptions(commandTimeoutSeconds), new ReadOptions(fieldsSelector, caseSensitive, fromTypeOption))
    {
    }
  }
}
