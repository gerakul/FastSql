namespace Gerakul.FastSql.Benchmarks;

// eight members of the usual types; the columns of TestData have the same names and types
public class Row
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public decimal Amount { get; set; }
    public DateTime Created { get; set; }
    public int? Age { get; set; }
    public bool Active { get; set; }
    public long Total { get; set; }
    public Guid Key { get; set; }
}

public struct RowStruct
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public decimal Amount { get; set; }
    public DateTime Created { get; set; }
    public int? Age { get; set; }
    public bool Active { get; set; }
    public long Total { get; set; }
    public Guid Key { get; set; }
}

public record RowRecord(int Id, string? Name, decimal Amount, DateTime Created, int? Age, bool Active, long Total, Guid Key);

// the same shape as Row: used to warm the library up before the first use of Row is measured
public class WarmUpRow
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public decimal Amount { get; set; }
    public DateTime Created { get; set; }
    public int? Age { get; set; }
    public bool Active { get; set; }
    public long Total { get; set; }
    public Guid Key { get; set; }
}

public static class TestData
{
    public static readonly string[] Names = ["Id", "Name", "Amount", "Created", "Age", "Active", "Total", "Key"];

    public static readonly Type[] Types = [typeof(int), typeof(string), typeof(decimal), typeof(DateTime), typeof(int), typeof(bool), typeof(long), typeof(Guid)];

    public static Row[] CreateRows(int count)
    {
        var rows = new Row[count];

        for (int i = 0; i < count; i++)
        {
            rows[i] = new Row()
            {
                Id = i,
                Name = "Name " + i,
                Amount = i * 1.5m,
                Created = new DateTime(2020, 1, 1).AddDays(i),
                Age = i % 3 == 0 ? null : i,
                Active = i % 2 == 0,
                Total = i * 1000L,
                Key = new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
            };
        }

        return rows;
    }

    // the rows as a reader returns them: a null value is null here and DBNull in the reader
    public static object?[][] CreateValues(int count)
    {
        return CreateRows(count)
            .Select(x => new object?[] { x.Id, x.Name, x.Amount, x.Created, x.Age, x.Active, x.Total, x.Key })
            .ToArray();
    }

    public static ArrayDataReader CreateReader(object?[][] values)
    {
        return new ArrayDataReader(values, Names, Types);
    }
}
