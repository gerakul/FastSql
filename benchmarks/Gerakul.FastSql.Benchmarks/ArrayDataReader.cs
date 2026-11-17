using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Gerakul.FastSql.Benchmarks;

// A reader over arrays that costs as little as possible itself, so the benchmarks measure FastSql
public sealed class ArrayDataReader : DbDataReader
{
    private readonly object?[][] values;
    private readonly string[] names;
    private readonly Type[] types;
    private int position = -1;

    public ArrayDataReader(object?[][] values, string[] names, Type[] types)
    {
        this.values = values;
        this.names = names;
        this.types = types;
    }

    public override bool Read()
    {
        return ++position < values.Length;
    }

    public override int FieldCount => names.Length;

    public override string GetName(int ordinal)
    {
        return names[ordinal];
    }

    public override Type GetFieldType(int ordinal)
    {
        return types[ordinal];
    }

    public override object GetValue(int ordinal)
    {
        return values[position][ordinal] ?? DBNull.Value;
    }

    public override bool IsDBNull(int ordinal)
    {
        return values[position][ordinal] == null;
    }

    public override int GetValues(object[] values)
    {
        var row = this.values[position];

        for (int i = 0; i < row.Length; i++)
        {
            values[i] = row[i] ?? DBNull.Value;
        }

        return row.Length;
    }

    public override int GetOrdinal(string name)
    {
        return Array.IndexOf(names, name);
    }

    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
    public override int Depth => 0;
    public override bool HasRows => values.Length > 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => -1;

    public override bool NextResult()
    {
        return false;
    }

    public override string GetDataTypeName(int ordinal)
    {
        return types[ordinal].Name;
    }

    public override IEnumerator GetEnumerator()
    {
        throw new NotSupportedException();
    }

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override char GetChar(int ordinal) => (char)GetValue(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
    public override string GetString(int ordinal) => (string)GetValue(ordinal);
}

// A command that returns an ArrayDataReader: for the query methods that start from a DbCommand
public sealed class ArrayCommand : DbCommand
{
    private readonly Func<ArrayDataReader> readerFactory;

    public ArrayCommand(Func<ArrayDataReader> readerFactory)
    {
        this.readerFactory = readerFactory;
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        return readerFactory();
    }

    [AllowNull]
    public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery()
    {
        throw new NotSupportedException();
    }

    public override object? ExecuteScalar()
    {
        throw new NotSupportedException();
    }

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter()
    {
        throw new NotSupportedException();
    }
}
