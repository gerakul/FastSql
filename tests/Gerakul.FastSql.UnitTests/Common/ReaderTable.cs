using System;
using System.Data;

namespace Gerakul.FastSql.UnitTests.Common
{
    // Builds the input of the mapping tests: a DataTable with typed columns, read through
    // DataTable.CreateDataReader() (a DbDataReader) or through InterfaceOnlyDataReader (only IDataReader).
    public sealed class ReaderTable
    {
        private readonly DataTable table = new DataTable();

        public ReaderTable Column(string name, Type type)
        {
            table.Columns.Add(name, type);
            return this;
        }

        public ReaderTable Column<T>(string name)
        {
            return Column(name, typeof(T));
        }

        // for column settings such as DateTimeMode (the default UnspecifiedLocal mode drops DateTime.Kind)
        public ReaderTable Column(DataColumn column)
        {
            table.Columns.Add(column);
            return this;
        }

        // null becomes DBNull
        public ReaderTable Row(params object[] values)
        {
            var row = table.NewRow();
            for (int i = 0; i < values.Length; i++)
            {
                row[i] = values[i] ?? DBNull.Value;
            }

            table.Rows.Add(row);
            return this;
        }

        public DataTableReader CreateReader()
        {
            return table.CreateDataReader();
        }

        // names, if given, replace the column names reported by the reader (DataTable rejects exact duplicates)
        public InterfaceOnlyDataReader CreateInterfaceOnlyReader(params string[] names)
        {
            return new InterfaceOnlyDataReader(table.CreateDataReader(), names);
        }
    }
}
