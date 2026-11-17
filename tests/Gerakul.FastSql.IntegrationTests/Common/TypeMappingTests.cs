using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // how the type mapping tests put values into the database
    public enum TypeMappingWritePath
    {
        // CommandCreatorExtensions.Insert: one parameter per member, the driver infers the parameter type from the CLR value
        Insert,

        // IEnumerable<T>.WriteToServer: SqlBulkCopy / Npgsql binary COPY over a UniversalDataReader
        BulkCopy
    }

    // Write and read-back mechanics for the type mapping tests (I6). The test classes are provider-only
    // (SqlServer/SqlServerTypeMappingTests, PostgreSQL/PostgreSqlTypeMappingTests), because the column types differ.
    // Everything runs on the connection string context: I6 is about types, not about context kinds.
    public abstract class TypeMappingTests<TDatabase> : IClassFixture<TDatabase> where TDatabase : DatabaseFixture
    {
        protected TypeMappingTests(TDatabase database)
        {
            this.Database = database;
        }

        protected TDatabase Database { get; }

        protected ProviderSpec Spec => Database.Spec;

        protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

        protected DbContext CreateContext()
        {
            return Spec.DefaultProvider.CreateContext(Database.ConnectionString);
        }

        protected string Quote(string identifier)
        {
            return Spec.Quote(identifier);
        }

        #region Round trip cases

        // theory rows (column type, write path): every case with every write path it supports
        protected static TheoryData<string, TypeMappingWritePath> RoundTripData(IEnumerable<TypeCase> cases)
        {
            var data = new TheoryData<string, TypeMappingWritePath>();
            foreach (var typeCase in cases)
            {
                foreach (var writePath in typeCase.WritePaths)
                {
                    data.Add(typeCase.ColumnType, writePath);
                }
            }

            return data;
        }

        protected static TypeCase FindCase(IEnumerable<TypeCase> cases, string columnType)
        {
            return cases.Single(x => x.ColumnType == columnType);
        }

        // creates a table for the case, writes its rows and returns the table name
        protected string WriteRows(TypeCase typeCase, TypeMappingWritePath writePath)
        {
            var table = CreateRowsTable(typeCase.ColumnType);
            typeCase.Write(CreateContext(), table, writePath);
            return table;
        }

        protected async Task<string> WriteRowsAsync(TypeCase typeCase, TypeMappingWritePath writePath)
        {
            var table = CreateRowsTable(typeCase.ColumnType);
            await typeCase.WriteAsync(CreateContext(), table, writePath);
            return table;
        }

        private string CreateRowsTable(string columnType)
        {
            return Database.CreateScratchTable($"{Quote("Id")} int not null, {Quote("Value")} {columnType} not null, "
                + $"{Quote("NullableValue")} {columnType} null");
        }

        // A column type with values of the CLR type the driver returns for it. Each value i is written as the row
        // (i, value, value), and one more row (n + 1, first value, null) covers the nullable twin. The values read back
        // as the expected values (padding, rounding, normalization), and ExecuteQuery() returns them as that CLR type.
        protected abstract class TypeCase
        {
            protected TypeCase(string columnType, TypeMappingWritePath[] writePaths)
            {
                this.ColumnType = columnType;
                this.WritePaths = writePaths;
            }

            public string ColumnType { get; }

            public TypeMappingWritePath[] WritePaths { get; }

            public abstract void Write(DbContext context, string table, TypeMappingWritePath writePath);

            public abstract Task WriteAsync(DbContext context, string table, TypeMappingWritePath writePath);

            public abstract void AssertExecuteQuery(DbContext context, ProviderSpec spec, string table);

            public abstract void AssertExecuteQueryAnonymous(DbContext context, ProviderSpec spec, string table);

            public abstract void AssertExecuteQueryFirstColumn(DbContext context, ProviderSpec spec, string table);

            public abstract void AssertExecuteQueryUntyped(DbContext context, ProviderSpec spec, string table);

            public abstract Task AssertReadAsync(DbContext context, ProviderSpec spec, string table);

            protected static string SelectRows(ProviderSpec spec, string table)
            {
                return $"select {spec.Quote("Id")}, {spec.Quote("Value")}, {spec.Quote("NullableValue")} from {table} order by {spec.Quote("Id")}";
            }

            protected static string SelectColumn(ProviderSpec spec, string table, string column)
            {
                return $"select {spec.Quote(column)} from {table} order by {spec.Quote("Id")}";
            }
        }

        // TNullable is the nullable twin: T? for value types, T itself for reference types
        protected sealed class TypeCase<T, TNullable> : TypeCase
        {
            private static readonly TypeMappingWritePath[] allWritePaths = new[] { TypeMappingWritePath.Insert, TypeMappingWritePath.BulkCopy };

            private readonly T[] values;
            private readonly T[] expected;

            public TypeCase(string columnType, T[] values, T[] expected = null, TypeMappingWritePath[] writePaths = null)
                : base(columnType, writePaths ?? allWritePaths)
            {
                this.values = values;
                this.expected = expected ?? values;
            }

            // the Value column of every row
            private T[] ExpectedValues => expected.Concat(new[] { expected[0] }).ToArray();

            // the NullableValue column of every row
            private TNullable[] ExpectedNullableValues => expected.Select(x => (TNullable)(object)x).Concat(new[] { default(TNullable) }).ToArray();

            public override void Write(DbContext context, string table, TypeMappingWritePath writePath)
            {
                var rows = CreateRows(values);
                if (writePath == TypeMappingWritePath.Insert)
                {
                    foreach (var row in rows)
                    {
                        context.Insert(table, row);
                    }
                }
                else
                {
                    rows.WriteToServer(context, table);
                }
            }

            public override async Task WriteAsync(DbContext context, string table, TypeMappingWritePath writePath)
            {
                var rows = CreateRows(values);
                if (writePath == TypeMappingWritePath.Insert)
                {
                    foreach (var row in rows)
                    {
                        await context.InsertAsync(table, row);
                    }
                }
                else
                {
                    await rows.WriteToServerAsync(context, table, CancellationToken);
                }
            }

            public override void AssertExecuteQuery(DbContext context, ProviderSpec spec, string table)
            {
                var rows = context.CreateSimple(SelectRows(spec, table)).ExecuteQuery<Row<T, TNullable>>().ToArray();

                rows.Should().BeEquivalentTo(CreateRows(expected), o => o.WithStrictOrdering());
            }

            public override void AssertExecuteQueryAnonymous(DbContext context, ProviderSpec spec, string table)
            {
                var rows = context.CreateSimple(SelectRows(spec, table))
                    .ExecuteQueryAnonymous(new { Id = 0, Value = default(T), NullableValue = default(TNullable) }).ToArray();

                rows.Should().BeEquivalentTo(CreateRows(expected), o => o.WithStrictOrdering());
            }

            public override void AssertExecuteQueryFirstColumn(DbContext context, ProviderSpec spec, string table)
            {
                var valueColumn = context.CreateSimple(SelectColumn(spec, table, "Value")).ExecuteQueryFirstColumn<T>().ToArray();
                var nullableValueColumn = context.CreateSimple(SelectColumn(spec, table, "NullableValue")).ExecuteQueryFirstColumn<TNullable>().ToArray();

                valueColumn.Should().BeEquivalentTo(ExpectedValues, o => o.WithStrictOrdering());
                nullableValueColumn.Should().BeEquivalentTo(ExpectedNullableValues, o => o.WithStrictOrdering());
            }

            public override void AssertExecuteQueryUntyped(DbContext context, ProviderSpec spec, string table)
            {
                AssertUntypedRows(context.CreateSimple(SelectRows(spec, table)).ExecuteQuery().ToArray());
            }

            public override async Task AssertReadAsync(DbContext context, ProviderSpec spec, string table)
            {
                var rows = await context.CreateSimple(SelectRows(spec, table))
                    .ExecuteQueryAsync<Row<T, TNullable>>(cancellationToken: CancellationToken).ToListCompat(CancellationToken);
                var anonymousRows = await context.CreateSimple(SelectRows(spec, table))
                    .ExecuteQueryAnonymousAsync(new { Id = 0, Value = default(T), NullableValue = default(TNullable) }, cancellationToken: CancellationToken)
                    .ToListCompat(CancellationToken);
                var valueColumn = await context.CreateSimple(SelectColumn(spec, table, "Value"))
                    .ExecuteQueryFirstColumnAsync<T>(CancellationToken).ToListCompat(CancellationToken);
                var nullableValueColumn = await context.CreateSimple(SelectColumn(spec, table, "NullableValue"))
                    .ExecuteQueryFirstColumnAsync<TNullable>(CancellationToken).ToListCompat(CancellationToken);
                var untypedRows = await context.CreateSimple(SelectRows(spec, table))
                    .ExecuteQueryAsync(CancellationToken).ToListCompat(CancellationToken);

                rows.Should().BeEquivalentTo(CreateRows(expected), o => o.WithStrictOrdering());
                anonymousRows.Should().BeEquivalentTo(CreateRows(expected), o => o.WithStrictOrdering());
                valueColumn.Should().BeEquivalentTo(ExpectedValues, o => o.WithStrictOrdering());
                nullableValueColumn.Should().BeEquivalentTo(ExpectedNullableValues, o => o.WithStrictOrdering());
                AssertUntypedRows(untypedRows.ToArray());
            }

            private void AssertUntypedRows(object[][] rows)
            {
                var expectedValues = ExpectedValues;
                rows.Should().HaveCount(expectedValues.Length);
                for (int i = 0; i < rows.Length; i++)
                {
                    rows[i][0].Should().Be(i + 1);
                    rows[i][1].Should().BeOfType<T>();
                    rows[i][1].Should().BeEquivalentTo(expectedValues[i]);
                    if (i < rows.Length - 1)
                    {
                        rows[i][2].Should().BeOfType<T>();
                        rows[i][2].Should().BeEquivalentTo(expectedValues[i]);
                    }
                    else
                    {
                        rows[i][2].Should().BeSameAs(DBNull.Value);
                    }
                }
            }

            private static Row<T, TNullable>[] CreateRows(T[] items)
            {
                return items.Select((x, i) => new Row<T, TNullable>() { Id = i + 1, Value = x, NullableValue = (TNullable)(object)x })
                    .Concat(new[] { new Row<T, TNullable>() { Id = items.Length + 1, Value = items[0], NullableValue = default(TNullable) } })
                    .ToArray();
            }
        }

        protected class Row<T, TNullable>
        {
            public int Id { get; set; }

            public T Value { get; set; }

            public TNullable NullableValue { get; set; }
        }

        #endregion

        #region Single values

        // a table with an "Id" and a nullable "Value" column, for edge values and failures
        protected string CreateValueTable(string columnType)
        {
            return Database.CreateScratchTable($"{Quote("Id")} int not null, {Quote("Value")} {columnType} null");
        }

        protected void WriteValue<T>(string table, TypeMappingWritePath writePath, T value)
        {
            WriteValue(CreateContext(), table, writePath, value);
        }

        protected static void WriteValue<T>(DbContext context, string table, TypeMappingWritePath writePath, T value)
        {
            var row = new ValueRow<T>() { Id = 1, Value = value };
            if (writePath == TypeMappingWritePath.Insert)
            {
                context.Insert(table, row);
            }
            else
            {
                new[] { row }.WriteToServer(context, table);
            }
        }

        // ExecuteQueryFirstColumn<T> of the single row
        protected T ReadValue<T>(string table)
        {
            return ReadValue<T>(CreateContext(), table);
        }

        protected T ReadValue<T>(DbContext context, string table)
        {
            return context.CreateSimple($"select {Quote("Value")} from {table}").ExecuteQueryFirstColumn<T>().Single();
        }

        // the value as the driver returns it (ExecuteQuery())
        protected object ReadRawValue(string table)
        {
            return CreateContext().CreateSimple($"select {Quote("Value")} from {table}").ExecuteQuery().Single()[0];
        }

        protected int CountRows(string table)
        {
            return CreateContext().CreateSimple($"select count(*) from {table}").ExecuteQueryFirstColumn<int>().Single();
        }

        protected class ValueRow<T>
        {
            public int Id { get; set; }

            public T Value { get; set; }
        }

        #endregion
    }
}
