# Test plan (modernization phase 0)

This is the detailed plan for covering FastSql with unit and integration tests before the
modernization. The tools and the reasons behind them are in [testing-approach.md](testing-approach.md).

The plan is also a checklist: each implementation PR ticks off the items it covers, and adds
any quirk it discovers to the [quirks register](#7-quirks-register).

Contents:

1. [Ground rules](#1-ground-rules)
2. [Solution layout](#2-solution-layout)
3. [Shared test infrastructure](#3-shared-test-infrastructure)
4. [Upgrade seams](#4-upgrade-seams)
5. [Unit tests](#5-unit-tests)
6. [Integration tests](#6-integration-tests)
7. [Quirks register](#7-quirks-register)
8. [CI](#8-ci)
9. [Coverage and definition of done](#9-coverage-and-definition-of-done)
10. [Delivery plan](#10-delivery-plan)
- [Appendix A. Public API coverage map](#appendix-a-public-api-coverage-map)

## 1. Ground rules

- **Characterization, not correction.** Tests pin what the `netstandard2.0` build does today.
  When a test exposes a bug, the test pins the current behavior, is tagged as a quirk, and the
  quirk is added to the register. No fixes in phase 0.
- **No library changes.** Tests use the public API, plus protected members through subclassing
  where the API is designed for extension (contexts, options). No `InternalsVisibleTo`.
  Internal classes (`ColumnDefinition`, `ReadInfo`, `AEnumerable`, the bulk copy implementations)
  are covered through the public features that use them.
- **Both providers, all context kinds, sync and async.** Every scenario that can run on both
  providers runs on both. Every command scenario runs with all three context kinds (connection
  string, connection, transaction), because they go through different internal code. Every
  method with an async twin is tested in both forms.
- **Independent tests.** No test depends on another test or on execution order. Seed data is
  read only; tests that modify data create their own scratch tables.
- **No shared mutable state.** `SqlContextProvider.DefaultInstance`,
  `NpgsqlContextProvider.DefaultInstance` and the static `ColumnDefinitionOptions` instances are
  mutable and shared. Tests that change options use `new SqlContextProvider()` /
  `new NpgsqlContextProvider()` and clones; tests that use the default instances never change them.

## 2. Solution layout

```
global.json                                  test runner = Microsoft Testing Platform
.github/workflows/tests.yml                  CI
tests/
  Directory.Build.props                      shared settings and packages for test projects only
  Shared/
    AsyncEnumerableCompat.cs                 the only code that knows the IAsyncEnumerable flavor
  Gerakul.FastSql.UnitTests/
    Infrastructure/                          capturing contexts, provider specs, test models
    Common/                                  mapping, readers, field settings, options, compilation
    SqlServer/                               SQL Server text generator, provider hooks, options
    PostgreSQL/                              PostgreSQL text generator, provider hooks, options
    PublicApi/                               API snapshot tests + Approved/*.txt
  Gerakul.FastSql.IntegrationTests/
    Infrastructure/                          container and database fixtures, context-kind helper
    Seeds/                                   SqlServer.sql, PostgreSql.sql (embedded resources)
    Common/                                  abstract scenario classes shared by both providers
    SqlServer/                               concrete SQL Server classes + SQL Server only tests
    PostgreSQL/                              concrete PostgreSQL classes + PostgreSQL only tests
```

- Both test projects are added to `FastSql.sln` under a `tests` solution folder.
- `tests/Directory.Build.props` applies only to the test projects, so library builds are not
  affected. It sets `net10.0`, `OutputType=Exe`, `IsPackable=false`, nullable reference types
  disabled (matching the library), and references the common packages: `xunit.v3`,
  `AwesomeAssertions`, `Microsoft.Testing.Extensions.CodeCoverage`, and
  `System.Interactive.Async` 3.2.0 with `Aliases="global,Ix"` (for the compat helper). It also
  links `tests/Shared/**/*.cs` into both projects.
- Each project has an `xunit.runner.json` with `"culture": "invariant"` (value conversions such as
  `DateTime` to `string` are culture dependent).
- The integration project references `Testcontainers.MsSql` and `Testcontainers.PostgreSql`;
  the unit project references `PublicApiGenerator`.
- Test code follows the library style: block-scoped namespaces, braces on their own lines,
  `this.` field assignments, classic constructors, no nullable annotations.

### Naming and tags

- Test names: `Method_Condition_ExpectedResult`, async tests end with `Async`.
- Namespaces mirror the folders: `Gerakul.FastSql.UnitTests.Common`, and so on.
- Traits:
  - `[Trait("Quirk", "Q07")]` on tests that pin a quirk from the register;
  - `[Trait("UpgradeSensitive", "<reason>")]` on tests expected to change with driver upgrades;
  - `[Trait("Provider", "SqlServer" | "PostgreSQL")]` on integration classes.
- Tests that pass a `CancellationToken` use `TestContext.Current.CancellationToken`, except tests
  about token behavior itself.

## 3. Shared test infrastructure

### 3.1 AsyncEnumerableCompat (both projects)

The library returns the legacy `IAsyncEnumerable<T>` from System.Interactive.Async 3.2
(`GetEnumerator()` / `MoveNext(CancellationToken)`). The helper names it through the `Ix` alias
and offers:

- `ToListCompat(source, cancellationToken)`: enumerate fully and dispose;
- `TakeCompat(source, count)`: enumerate partially, then dispose (for resource tests);
- `MoveNextCompat(enumerator, token)` and `GetEnumeratorCompat(source)`: for tests about
  cancellation and disposal semantics.

After the upgrade only this file is rewritten (to `await foreach` / `GetAsyncEnumerator`).

### 3.2 Capturing contexts (unit)

`CapturingSqlConnectionContext : SqlConnectionContext` and
`CapturingNpgsqlConnectionContext : NpgsqlConnectionContext` override the public
`CreateCommand(string)`, call the base implementation and keep the created `DbCommand`. They
wrap an **unopened** connection, so no database is needed.

Scoped contexts build the command as soon as a `Create*` method is called, so every `Create*`
overload can be inspected without executing anything: `CommandText`, `CommandType`,
`CommandTimeout`, and each parameter's name, value and `DbType`. This goes through the real
`DbContext.Set` path, and it works for `ISqlCommandCreator`, because the subclass is still a SQL
Server context.

### 3.3 Provider specs (both projects)

A small `IProviderSpec` per provider holds what differs between providers in tests:

- provider factory (`new SqlContextProvider()` / `new NpgsqlContextProvider()`);
- identifier quoting (`[Name]` / `"Name"`);
- creating parameters through `DbCommand.CreateParameter()` (name, value, direction);
- integration only: SQL snippets for "sleep N seconds", "current isolation level",
  "count my sessions", and scratch table DDL.

Shared test classes are abstract and generic over the spec; each provider has a thin concrete
class.

### 3.4 Containers (integration, assembly fixtures)

- `SqlServerContainerFixture` and `PostgreSqlContainerFixture` start one container each per test
  run (`[assembly: AssemblyFixture(...)]`) with the pinned images, and expose an admin connection
  string.
- If Docker is not available, the integration tests fail with a clear message. They are not
  skipped, so a missing database is never mistaken for a pass.

### 3.5 Databases (integration, class fixtures)

- `SqlServerDatabase` and `PostgreSqlDatabase` (class fixtures) receive the container fixture in
  their constructor (verified in the prototype), create a database with a unique name, run the
  seed script, and expose its connection string.
- Each test class therefore has its own database. Classes run in parallel, tests inside a class
  run one at a time (the xUnit default).
- `CreateScratchTable(columnsDdl)` creates a table with a unique name for tests that modify data.

### 3.6 Seed data

Ported from `Examples/InitQuery.sql`, so the tests follow the README examples:

- `Company` (ID, Name, DateOfFoundation): 2 rows.
- `Employee` (identity ID, CompanyID, Name, Phone, Age, StartWorking): 5 rows with the same
  values as the example script, including nulls.
- `TestProc(@CompanyID, @Age)`: returns employees of a company older than an age.

The PostgreSQL seed uses quoted PascalCase identifiers (`"Employee"`, `"CompanyID"`), so the same
C# models work on both providers (the PostgreSQL text generator quotes column names, see Q19).
`TestProc` becomes a function returning `setof "Employee"`. Npgsql 4 calls it as
`select * from TestProc("CompanyID" => ..., "Age" => ...)`: the function name is not quoted (it folds
to lower case) but the parameter names are, so the function declares quoted parameter names
(found while building the infrastructure; relevant to I8, `UpgradeSensitive`).

### 3.7 Context kinds (integration)

`ContextKind { ConnectionString, Connection, Transaction }` with helpers
`db.Run(kind, ctx => ...)` and `db.RunAsync(kind, async ctx => ...)`:

- ConnectionString: `provider.CreateContext(connectionString)`;
- Connection: opens a connection, `provider.CreateContext(connection)`, disposes it afterwards;
- Transaction: opens a connection, begins a transaction, `provider.CreateContext(transaction)`,
  commits afterwards (tests about rollback do it themselves).

Scenario tests are theories over `ContextKinds.All`.

### 3.8 Session counting (integration)

To check that connections are not leaked, a connection string with `Pooling=false` and a unique
application name is used. An admin connection counts sessions with that name
(`sys.dm_exec_sessions` / `pg_stat_activity`).

### 3.9 Test models

`Employee` and `Company` from the examples, plus models for mapping tests: public fields vs
properties, nullable and non-nullable value types, enums, get-only properties, collection
properties, indexers, inherited members, and members differing only by case.

## 4. Upgrade seams

| Expected phase 1 change | Test impact | How the tests limit it |
|---|---|---|
| System.Interactive.Async 3.2 to the built-in `IAsyncEnumerable<T>` | Every async query test | Only `AsyncEnumerableCompat` changes. The README scenario lines that use `await ... .ToArray()` change together with the README. |
| System.Data.SqlClient to Microsoft.Data.SqlClient | Tests using `SqlParameter`, `SqlConnection`, `SqlBulkCopyOptions` | These live only in `SqlServer/` folders and the SQL Server spec. Microsoft.Data.SqlClient encrypts by default; the Testcontainers connection string already trusts the server certificate. |
| Npgsql 4 to a current version | Type mapping (`timestamptz` returns local `DateTime` in Npgsql 4, UTC in 6+), stored procedure calls (Npgsql 7 calls procedures, not functions), binary COPY type inference | Tagged `UpgradeSensitive`; expectations are asserted in a time-zone-independent way (instant and `Kind` separately). |
| Public API changes (async types, driver types) | Public API snapshot | Approved files are updated deliberately in the upgrade PR. |
| Dropping `netstandard1.3` / `net46` | None | Not tested. |

## 5. Unit tests

Unit tests need no Docker. Classes marked *(per provider)* are abstract and run for both providers.

### U1. SQL text generation

Classes: `SqlCommandTextGeneratorTests`, `NpgsqlCommandTextGeneratorTests`.

- [ ] `ColumnList`: one, several, none; quoting (`[x]` / `"x"`); quote characters inside names are not escaped (Q20).
- [ ] `ParamList`, `WhereClause` (joined with `and`), `SetClause`.
- [ ] `Insert`; `InsertWithOutput` (SQL Server `output inserted.[x]` before `values`, PostgreSQL `returning "x"`).
- [ ] `InsertAndGetID` (SQL Server only): insert followed by `select scope_identity();`.
- [ ] `Update(table, whereClause, fields)` and the base `Update(table, keyFields, fields)`.
- [ ] `Delete(table, whereClause)` and the base `Delete(table, params keyFields)`, including the overload binding of `Delete("T", "ID")` (Q07).
- [ ] `Merge`: single key, composite key, key repeated in fields (deduplicated), no non-key fields (Q09); SQL Server `MERGE` vs PostgreSQL `insert ... on conflict ... do update`; lines separated by `Environment.NewLine`.
- [ ] Table names are used verbatim (schema-qualified, bracketed, quoted).

### U2. Provider hooks, through observable behavior

Classes: `SqlContextProviderTests`, `NpgsqlContextProviderTests` (capturing contexts).

- [ ] Parameter discovery from command text (`CreateMapped` without explicit names): `@a`; repeated `@a` (one parameter); `@@ROWCOUNT` ignored; parameter at the start of the text and after a newline; names with digits and `$ # _`; `(@a,@b)`; `@` inside a string literal or an e-mail address is treated as a parameter (Q11).
- [ ] Explicit parameter names with and without `@`: matching ignores the prefix, the parameter keeps the name as given.
- [ ] Null values: SQL Server sends `SqlBytes.Null` for `byte[]` members and `DBNull` otherwise; PostgreSQL always `DBNull`.
- [ ] Parameter `DbType` inferred by `AddWithValue` for common CLR types (string, int, long, decimal, bool, DateTime, DateTimeOffset, Guid, byte[], enum) (`UpgradeSensitive`).
- [ ] Stored procedure parameters come from all settings, named `@` + member name.
- [ ] `CommandTimeout` is set only when a timeout option has a value; otherwise the driver default stays.

### U3. Field settings

Class: `FieldSettingsTests`.

- [ ] Each of the 5 public constructors: `Name`, `DataTypeName` (defaults to `FieldType.Name`), `FieldType`, `Getter`, `IsNullGetter`, `DetermineNullByValue`.
- [ ] `FieldSettings<T>(name)`: identity getter for sequences of scalars.
- [ ] `FieldSettings.FromType<T>(option)`: public fields first, then properties; `PublicField`, `PublicProperty`, `Default`/`Both`, `Collection`, `All`.
- [ ] Collections (`List<int>`, `int[]`, `IEnumerable<string>`, dictionaries) are excluded unless `Collection` is set; `string` and `byte[]` are always included.
- [ ] Static and non-public members excluded; inherited members included; an indexer property is included and its getter throws when invoked (Q21).
- [ ] `FromType(proto)` (anonymous types), `GetFields<T>()`, `GetFields(proto)`, `GetNames()`, `IEnumerable<T>.GetFieldSettings()`.

### U4. Options

Classes: `QueryOptionsTests`, `ReadOptionsTests`, `BulkOptionsTests`, `SqlBulkOptionsTests`, `NpgsqlBulkOptionsTests`, `ColumnDefinitionOptionsTests` (both namespaces).

- [ ] Every public constructor stores its arguments (including all 10 `SqlBulkOptions` arguments; `NpgsqlBulkOptions` leaves `CreateTable` and the other base options unset).
- [ ] `Clone()` copies every property, returns the same runtime type, returns an independent instance; `SqlBulkOptions.Clone()` shares the `ColumnDefinitionOptions` instance (shallow).
- [ ] Provider defaults: `QueryOptions`, `BulkOptions`, `ReadOptions` of both providers are the provider-specific types with all values unset.
- [ ] Protected defaults (through test subclasses): `QueryOptions` (timeout unset, case-insensitive parameters); `BulkOptions` (`Source`, case sensitivity unset, no table creation, schema table used, no existence check); `SqlBulkOptions` (`SqlBulkCopyOptions.Default`, `ColumnDefinitionOptions.Default`).
- [ ] `SetDefaults(other)` (through test subclasses) fills only unset values; `SqlBulkOptions` takes SQL Server values only from another `SqlBulkOptions` and clones its `ColumnDefinitionOptions`.
- [ ] Protected copy constructors (through test subclasses): SQL Server / PostgreSQL options built from base options copy the base values; `SqlBulkOptions` copies the SQL Server values from another `SqlBulkOptions` and leaves them unset otherwise.
- [ ] Timeout precedence on the generated command: none, provider level, context level (`ctx.QueryOptions.CommandTimeoutSeconds = n`), per command (wins); a base `QueryOptions` passed to a SQL Server context works.
- [ ] Context creation clones the provider options: later changes on either side do not leak.
- [ ] Case-sensitive parameter matching precedence: default (insensitive), context level, per command.
- [ ] `ColumnDefinitionOptions`: `Default`, `Max`, `Small` values; `Clone()` copies the primary key list; `FromOption`, `FromDefault`, `FromMax`, `FromSmall` set the primary key and do not change the static source; `ThrowIfUnsupportedType` is false by default.

### U5. Mapping from data readers

Classes: `ReadAllTests`, `ReadAllAsyncTests`, `ReadAllAnonymousTests`, `ReadAllFirstColumnTests`, `ReadAllObjectsTests`, `ValueConversionTests`, `FieldsSelectorTests`.

Input comes from `DataTable.CreateDataReader()` and from a wrapper that implements only
`IDataReader` (to reach the non-`DbDataReader` async path).

- [ ] `ReadAll<T>`: properties and public fields; `FromTypeOption` (fields only, properties only, `Collection` maps an array column); case-insensitive by default; case-sensitive option; column order does not matter.
- [ ] `FieldsSelector` × (exact match, extra column, extra member, no overlap) for `Destination` (default), `Source`, `Common`, `Both`: rows mapped, or `InvalidOperationException` with the current message; an undefined selector value gives `ArgumentException`.
- [ ] Duplicates: duplicate reader columns (case-insensitive and case-sensitive modes); a field and a property whose names differ only by case; `ArgumentException` with the current messages.
- [ ] Nulls: `DBNull` becomes null for reference and `Nullable<T>` members; `DBNull` into a non-nullable value type throws `InvalidCastException` (Q12).
- [ ] Conversions (regression set for commit 41af0f9): int→long, long→int, int→decimal, decimal→double, short→int?, string "42"→int, int→string, bool, Guid, byte[], DateTime, DateTimeOffset, TimeSpan; enum from int and from string (case-sensitive, invalid name throws `ArgumentException`); nullable enum from `DBNull`; Guid→string throws `InvalidCastException`.
- [ ] Get-only property: required by the `Destination` selector, then fails when set (Q13).
- [ ] Laziness: mapping errors appear when enumerating, not when calling `ReadAll`; enumerating the result a second time yields nothing (forward-only reader).
- [ ] `ReadAllAnonymous`: matching by constructor parameter names; missing columns with `Common` give default values; `Destination` requires all parameters; case sensitivity; conversions; the first constructor is used for non-anonymous types.
- [ ] `ReadAllFirstColumn()`: `DBNull` becomes null, while `ReadAll()` keeps `DBNull` in its arrays (Q12); `ReadAllFirstColumn<T>()`: conversion, nullable, enum.
- [ ] `ReadAll()`: array length equals `FieldCount`.
- [ ] `GetColumnNames()`.
- [ ] Async twins of all of the above return the same results; the `IDataReader`-only path works; a cancelled token makes `MoveNext` return false instead of throwing (Q04); empty reader; disposing the enumerator.

### U6. UniversalDataReader and ToDataReader

Classes: `UniversalDataReaderTests`, `ToDataReaderTests`.

- [ ] All 8 `ToDataReader` overloads (`IEnumerable` and legacy `IAsyncEnumerable`, × settings array / settings + case sensitivity / `FromTypeOption` / defaults): resulting `Settings`; case-sensitive by default.
- [ ] Metadata: `FieldCount`, `GetName`, `GetOrdinal` (case-sensitive and insensitive; unknown name throws `KeyNotFoundException`), `GetFieldType`, `GetDataTypeName` (CLR type name or custom), `Depth` = 0.
- [ ] Values: `Read` and `ReadAsync` over both source kinds; `GetValue`, `GetValues` (shorter and longer arrays), both indexers, `IsDBNull` (custom null predicate), typed getters (exact cast: `GetInt64` on an int throws `InvalidCastException`), `GetBytes` / `GetChars` (offsets, lengths, out-of-range returns 0).
- [ ] Lifecycle: `Close()` on the reader sets `IsClosed` and `Read` then throws `InvalidOperationException`; `Close()` through a `DbDataReader` reference does not close it (Q01); `Dispose()` closes.
- [ ] Not implemented: `NextResult`, `RecordsAffected`, `HasRows` throw `NotImplementedException` (Q02); `GetEnumerator()` returns the source enumerator, or null for an async source (Q03); `GetSchemaTable()` / `GetColumnSchema()` throw `NotSupportedException` (leads to Q14).
- [ ] Duplicate names (in case-insensitive mode) throw `ArgumentException` at construction.
- [ ] Round trip: `ToDataReader().ReadAll<T>()` returns equal objects (sync and async).

### U7. Command compilation *(per provider)*

Class: `CommandCompilatorTests`. Each compiled command is turned into a `DbCommand` through
`CreateSimple(precompiled)` / `CreateMapped(precompiled)` on a capturing context.

- [ ] `CompileSimple`, `CompileProcedureSimple`: `CommandText`, `CommandType`.
- [ ] `CompileMapped` (6 overloads): names from text vs explicit list; `FromTypeOption`; proto overloads; settings overloads; case-sensitive and insensitive matching; parameter without a setting throws `ArgumentException`; duplicate parameter names and duplicate settings throw `ArgumentException`; null parameter list throws `NullReferenceException` (Q17).
- [ ] `CompileProcedure` (6 overloads): stored procedure type; parameters from all settings or from an explicit subset.
- [ ] `CompileInsert` (5 overloads): fields minus ignored fields; text from the generator; parameters match.
- [ ] `CompileInsertWithOutput`: output fields; no output fields gives a plain insert; null ignore list.
- [ ] `CompileUpdate` (10 overloads): keys in `where`, other fields in `set`, `notKeyIgnoreFields` excluded; a key missing from the settings leaves its parameter unmapped (pinned).
- [ ] `CompileDelete` (5 overloads): explicit keys; no keys uses all members as keys.
- [ ] `CompileMerge` (10 overloads).
- [ ] Using a command compiled by one provider instance with a context of another provider instance throws `ArgumentException` (simple and mapped).
- [ ] `new CommandCompilator(provider)` behaves like `provider.CommandCompilator`; `context.CommandCompilator` is the provider's.
- [ ] Parameter values: null members become the provider's null; custom `IsNullGetter`; a member whose value is a `DbParameter` is added as is; simple commands name arguments `@p0`, `@p1`, ... by position (a `DbParameter` argument is added as is and still uses up its position), null becomes `DBNull`.

### U8. Command creator extensions *(per provider)*

Class: `CommandCreatorExtensionsTests`.

- [ ] Each of the 43 `CommandCreatorExtensions` overloads in Common produces the expected command (text, type, parameters, timeout), and passes on its options (`queryOptions`; default `fromTypeOption` is `Default` for `CreateMapped` and `Both` for `CreateProcedure`).
- [ ] `Insert`, `Update`, `Delete`, `Merge` and their async twins build the right command before executing (execution on an unopened connection throws `InvalidOperationException`; real execution is in I7).
- [ ] `CreateInsertAndGetID` (2 overloads, SQL Server): command text and parameters.
- [ ] Contexts returned by `CreateContext` are typed as the base classes, so `CreateInsertAndGetID` needs a cast; SQL Server contexts implement `ISqlCommandCreator` at runtime, PostgreSQL contexts have no equivalent.
- [ ] Custom extensions through `ICommandCreator.Set(...)` (README "Extensibility") work on any context.
- [ ] Scoped contexts build the command immediately and read member values at that moment (unit part of Q15).

### U9. Providers and contexts without a database

Class: `ContextProviderTests`.

- [ ] `DefaultInstance` is a singleton; new instances are independent.
- [ ] Provider members: `CommandTextGenerator`, `QueryOptions`, `BulkOptions`, `ReadOptions`, `CommandCompilator` (types and identity).
- [ ] `CreateContext(string)` gives the provider's connection string context with `ConnectionString`; `CreateContext(DbConnection)` with an unopened connection gives the connection context; a connection of the other provider throws `InvalidCastException`.
- [ ] Public constructors of all six context classes take the provider's options.
- [ ] `DbContext.ContextProvider` and `DbContext.CommandCompilator`.

### U10. Public API snapshot

Class: `PublicApiTests`.

- [ ] For each of the three assemblies, the API generated by `PublicApiGenerator` equals `PublicApi/Approved/<Assembly>.txt`. On a mismatch the test writes `<Assembly>.received.txt` next to it and fails with a hint to diff the two files.

## 6. Integration tests

Matrix shorthand: **P** = both providers, **K** = all three context kinds, **S/A** = sync and async.

### I1. Infrastructure smoke (P)

- [ ] Container starts; per-class database is created; seed data is present (2 companies, 5 employees, `TestProc` callable).

### I2. Context lifecycle and transactions (P)

Class: `ContextLifecycleTests`.

- [ ] `UsingConnection` / `UsingConnectionAsync`: the connection is open inside and closed afterwards; an exception propagates and the connection is still closed.
- [ ] `UsingTransaction` / `UsingTransactionAsync`, with and without an isolation level, on connection string and connection contexts: commit on success; rollback and rethrow on exception; the isolation level reaches the server; `Unspecified` gives the server default.
- [ ] Nested `UsingConnection` then `UsingTransaction`.
- [ ] `CreateContext(DbTransaction)` and the transaction context constructors: commands run inside the transaction; a rollback by the caller discards them.
- [ ] A connection context over a closed connection throws `InvalidOperationException` (the library does not open it).
- [ ] Child contexts inherit the parent context's options, not the provider's.

### I3. Query execution (P × K × S/A)

Class: `WrappedCommandQueryTests`.

- [ ] `ExecuteQuery<T>`, `ExecuteQuery()`, `ExecuteQueryAnonymous`, `ExecuteQueryFirstColumn()`, `ExecuteQueryFirstColumn<T>()`, `ExecuteScalar`, `ExecuteNonQuery` (rows affected) and their async twins, against the seed data.
- [ ] Parameters: positional `@p0`, `@p1`; null argument; mapped from anonymous and named types; explicit names; settings; precompiled simple and mapped commands reused across contexts of the same provider.
- [ ] `UseReader`, `UseReaderAsync` (both overloads): reader open inside, closed afterwards; multiple result sets reachable with `NextResult`; `ExecuteQuery*` reads only the first result set.
- [ ] `ReadOptions` precedence: per call, context level, provider level.
- [ ] Executing the same `IWrappedCommand` twice; member values read at execution (connection string context) vs at creation (scoped contexts) (Q15).
- [ ] Empty result sets.
- [ ] Server errors surface as `DbException`; the concrete exception type is recorded (`UpgradeSensitive`).

### I4. Raw DbCommand extensions (P × S/A)

Class: `DbCommandExtensionsTests`.

- [ ] All 10 `DbCommandExtensions` methods on a provider `DbCommand`.
- [ ] The reader is disposed after full enumeration; disposing early closes it, and the connection can run another command.

### I5. Execution semantics: laziness, resources, cancellation, timeouts (P)

Class: `ExecutionSemanticsTests`.

- [ ] Laziness: query methods return without touching the database; invalid SQL fails only on enumeration.
- [ ] No leaked sessions on connection string contexts after full enumeration, partial enumeration then dispose, an exception during enumeration, `UseReader`, bulk copy, and the async variants; exactly one extra session while an enumeration is open.
- [ ] Cancellation: a cancelled token on `ExecuteQueryAsync`, `ExecuteScalarAsync` (scoped and connection string contexts); `ExecuteNonQueryAsync` on a connection string context and `UseReaderAsync` on scoped contexts ignore the token (Q06); `MoveNext` with a cancelled token returns false (Q04).
- [ ] Command timeout: `QueryOptions(1)` with a 3 second server sleep fails with a `DbException` (detail `UpgradeSensitive`).

### I6. Type mapping (per provider, `UpgradeSensitive`)

Classes: `SqlServerTypeMappingTests`, `PostgreSqlTypeMappingTests`.

For each type (with a nullable twin): write through parameters (`Insert`) and through bulk copy,
read back through `ExecuteQuery<T>`, `ExecuteQueryAnonymous`, `ExecuteQueryFirstColumn<T>` and
`ExecuteQuery()` (recording the CLR type the driver returns).

- [ ] SQL Server: bit, tinyint, smallint, int, bigint, decimal, numeric, money, float, real, date, time, datetime, datetime2, smalldatetime, datetimeoffset, uniqueidentifier, char, varchar, nchar, nvarchar, nvarchar(max), varbinary, varbinary(max), xml (as string), rowversion (as byte[]).
- [ ] PostgreSQL: boolean, smallint, integer, bigint, numeric, real, double precision, date, time, timestamp, timestamptz (instant and `Kind` asserted separately), interval, uuid, char, varchar, text, bytea, json and jsonb (as string), int[] and text[] (with `FromTypeOption.Collection`), a PostgreSQL enum type read into a C# enum.
- [ ] Enums stored as int and as string; enum values written as parameters.
- [ ] Edge values: min/max, decimal precision and scale, unicode, empty string vs null, empty byte array, `DateTime.MinValue` into `datetime` (error pinned).

### I7. Data modification (P × K × S/A)

Classes: `InsertTests`, `InsertWithOutputTests`, `UpdateTests`, `DeleteTests`, `MergeTests`, `SqlServerInsertAndGetIdTests`.

- [ ] `Insert`: all columns; ignored fields (identity); nulls; `queryOptions` overload; named and anonymous types; table name forms (`dbo.Employee`, `[Employee]`, `"Employee"`, `public."Employee"`); PostgreSQL table with lower-case columns and PascalCase members fails (Q19).
- [ ] `CreateInsertWithOutput` (4 overloads): one and several output fields, read through `ExecuteQueryFirstColumn<int>` and `ExecuteQuery<T>`; ignored fields; no output fields gives a plain insert.
- [ ] `CreateInsertAndGetID` (SQL Server): `ExecuteScalar` returns the identity as `decimal` (Q18); `ExecuteQueryFirstColumn<int>`.
- [ ] `Update`: single and composite keys; rows affected; no matching row gives 0; nulls written.
- [ ] `Delete`: by keys; by all members when no keys are given; the README shortcut `new { ID = 2 }`; a null key value matches nothing (Q08).
- [ ] `Merge`: inserts when missing, updates when present, composite keys; PostgreSQL without a unique constraint on the keys fails (pinned); an object with only key members fails (Q09).
- [ ] Each method's async twin.

### I8. Stored procedures and functions (P)

Class: `StoredProcedureTests`.

- [ ] SQL Server: `TestProc` through `CreateProcedure`; an extra member fails with a server error (pinned); explicit parameter subset; settings overloads; `CreateProcedureSimple` with input, output and return value parameters (values read after execution); a `DbParameter` member used as an output parameter; a procedure with several result sets through `UseReader`.
- [ ] PostgreSQL (`UpgradeSensitive`): a function returning a table through `CreateProcedure` (named notation, member order does not matter); `CreateProcedureSimple` with parameters from `DbCommand.CreateParameter()`; a void function through `ExecuteNonQuery`; calling a `procedure` fails with Npgsql 4 (pinned).

### I9. Bulk copy and data import (P × K × S/A)

Classes: `BulkCopyTests`, `SqlServerBulkCopyOptionsTests`, `PostgreSqlBulkCopyTests`, `CrossProviderImportTests`.

- [ ] Every `WriteToServer` / `WriteToServerAsync` overload on `IEnumerable<T>` (6), `DbDataReader` (6) and `IWrappedCommand` (6), into all three destination context kinds; a transaction rollback discards the copied rows.
- [ ] `fields` subset; all source columns by default; empty source.
- [ ] Default `Source` selector maps by name; SQL Server matching is case-sensitive (Q16).
- [ ] With `CaseSensitiveFieldsMatching` set (true or false) the destination columns are read and joined; `Destination`, `Common`, `Both` with mismatches throw `InvalidOperationException`.
- [ ] Nulls; identity columns are generated.
- [ ] `SqlBulkCopyOptions`: `KeepIdentity`, `KeepNulls` (column default), `CheckConstraints`, `FireTriggers`; `TableLock`, `BatchSize`, `BulkCopyTimeout`, `EnableStreaming` accepted (smoke).
- [ ] SQL Server `CreateTable`: from a query reader (column types, lengths, nullability, primary key reproduced, checked in `INFORMATION_SCHEMA`); from `IEnumerable<T>` only with `IgnoreDataReaderSchemaTable = true`, otherwise `NotSupportedException` (Q14); `ColumnDefinitionOptions` `Default` / `Max` / `Small` and a primary key list; an unsupported member type with `ThrowIfUnsupportedType` false and true; `CheckTableIfNotExistsBeforeCreation` with an existing table; an `xml` source column throws `NotSupportedException`.
- [ ] PostgreSQL `CreateTable` throws `NotImplementedException` (sync and async) (Q10).
- [ ] Cross-provider import: a SQL Server query written into a PostgreSQL table and the reverse, through `IWrappedCommand.WriteToServer`.

### I10. README and Examples scenarios (P)

Class: `ReadmeScenarioTests`.

- [ ] Every README snippet, in order: simple example; context creation from a connection string, a connection and a transaction; `UsingConnection` / `UsingTransaction` and async variants; queries `data1` to `data11`; options; anonymous types; first column; non-query; insert and insert with output; update; merge; delete; bulk copy; stored procedures; data import; the extensibility examples (`CreateSelectTop10Employees`, with `limit 10` on PostgreSQL, and `GetTable<T>`).
- [ ] The async lines keep the README syntax (`await ... .ToArray()`) on purpose: they change together with the README in phase 1.

## 7. Quirks register

Behavior found while reading the code and in the prototype. Each item gets a pinning test tagged
`[Trait("Quirk", "Qnn")]`. Items marked "confirmed" were reproduced in the prototype.

| ID | Behavior | Area | Status |
|---|---|---|---|
| Q01 | `UniversalDataReader.Close()` hides `DbDataReader.Close()` instead of overriding it: closing through a `DbDataReader` reference leaves the reader open. | U6 | confirmed |
| Q02 | `UniversalDataReader.NextResult`, `RecordsAffected`, `HasRows` throw `NotImplementedException`. | U6 | from code |
| Q03 | `UniversalDataReader.GetEnumerator()` returns the source `IEnumerator<T>` (rows are `T`, not records), or null for an async source. | U6 | confirmed |
| Q04 | Legacy async enumerators return false from `MoveNext` when the token is cancelled, instead of throwing. | U5, I5 | confirmed |
| Q05 | PostgreSQL `WriteToServerAsync` performs the copy synchronously. | I9 | from code |
| Q06 | Cancellation token ignored by `ExecuteNonQueryAsync` on connection string contexts and by `UseReaderAsync` on scoped contexts; connection string contexts open the connection without the token for non-query, scalar, `UseReaderAsync` and bulk copy (async queries do pass it). | I5 | from code |
| Q07 | `CommandTextGenerator.Delete("T", "ID")` binds to the `whereClause` overload and produces `delete from T where ID;`. | U1 | confirmed |
| Q08 | `Delete` with a null key value compares with `= NULL` and deletes nothing. | I7 | from code |
| Q09 | `Update` / `Merge` with no non-key members produce invalid SQL. | U1, I7 | from code |
| Q10 | PostgreSQL bulk copy with `CreateTable` throws `NotImplementedException`; the public PostgreSQL `ColumnDefinitionOptions` is unused (copy of the SQL Server one). | U4, I9 | from code |
| Q11 | Parameter discovery treats `@` inside string literals and e-mail addresses as parameters. | U2 | from code |
| Q12 | `DBNull` into a non-nullable value type throws `InvalidCastException`; `ReadAll()` keeps `DBNull` while `ReadAllFirstColumn()` returns null. | U5 | confirmed |
| Q13 | Get-only properties count as destination members when reading, then fail when set. | U5 | from code |
| Q14 | SQL Server bulk copy of `IEnumerable<T>` with `CreateTable` throws `NotSupportedException` unless `IgnoreDataReaderSchemaTable` is true. | U6, I9 | confirmed |
| Q15 | Member values are read when the command is executed on connection string contexts, but when it is created on scoped contexts; scoped contexts reuse one `DbCommand` for repeated executions. | U8, I3 | confirmed |
| Q16 | SQL Server bulk copy with the default `Source` selector maps columns case-sensitively (`name` does not match `Name`). | I9 | confirmed |
| Q17 | Compiling a mapped command with a null parameter list throws `NullReferenceException`. | U7 | from code |
| Q18 | `CreateInsertAndGetID(...).ExecuteScalar()` returns the identity as `decimal`; `CreateContext` returns base types, so the method needs a cast to `ISqlCommandCreator`. | U8, I7 | confirmed |
| Q19 | The PostgreSQL text generator quotes column names but not table names, so members must match column case exactly while table names need manual quoting. | U1, I7 | from code |
| Q20 | Identifier quoting does not escape quote characters inside names. | U1 | from code |
| Q21 | Indexer properties are included by `FieldSettings.FromType` and their getters throw when invoked. | U3 | from code |

## 8. CI

Workflow `.github/workflows/tests.yml`:

- Triggers: pull requests into `modernization` and `master`, pushes to them, manual run.
- Concurrency group per branch, cancelling superseded runs.
- Jobs on `ubuntu-latest` with .NET 10 (`actions/setup-dotnet`) and a NuGet cache:
  - **build**: builds the whole solution (all current library targets, so build breaks are caught).
  - **unit**: `dotnet test` on the unit project with coverage.
  - **integration**: `dotnet test` on the integration project with coverage (Docker is preinstalled; images are pulled on first use, about one minute).
- Artifacts: test results (TRX) and coverage (Cobertura) for every run.
- Job time limits so a hung container cannot block the queue.

## 9. Coverage and definition of done

- Coverage is collected on every CI run and used to find gaps, not as a pass/fail gate.
  Expectation: every public member is exercised, and any uncovered line in the three assemblies
  can be explained (dead code or unreachable in practice).
- [Appendix A](#appendix-a-public-api-coverage-map) maps every public type to the areas that cover
  it; the approved API files from U10 are the exact member list.
- Phase 0 is done when all checklist items in this plan are ticked, CI is green on
  `modernization`, and you have reviewed the quirks register.

Rough size (to be refined while implementing): about 450 to 550 unit test cases and 700 to 1,000
integration test cases, counting theory rows and the provider / context kind / sync-async
combinations. Expected run time: unit tests under 15 seconds, integration tests a few minutes in
CI including image pulls.

## 10. Delivery plan

Each PR has its own branch off `modernization`, its own worktree, and targets `modernization`.

| # | Branch | Content | Depends on |
|---|---|---|---|
| 0 | `tests/plan` | This plan and the approach document | – |
| 1 | `tests/infrastructure` | Both test projects, `tests/Directory.Build.props`, `global.json`, shared helpers (3.1 to 3.9), seed scripts, I1 smoke tests, U10 public API snapshot, CI workflow | 0 |
| 2 | `tests/unit-text-and-options` | U1, U2, U4 | 1 |
| 3 | `tests/unit-field-settings-and-reader` | U3, U6 | 1 |
| 4 | `tests/unit-mapping` | U5 | 1 |
| 5 | `tests/unit-commands` | U7, U8, U9 | 1 |
| 6 | `tests/integration-execution` | I2, I3, I4, I5 | 1 |
| 7 | `tests/integration-type-mapping` | I6 | 1 |
| 8 | `tests/integration-modification` | I7, I8 | 1 |
| 9 | `tests/integration-bulk-copy` | I9 | 1 |
| 10 | `tests/integration-readme` | I10, coverage gap review, final quirks register | 2 to 9 |

After PR 1 is merged, PRs 2 to 9 are independent of each other and could be worked on in
parallel.

## Appendix A. Public API coverage map

Member counts are public and protected members as reported by `PublicApiGenerator`
(about 445 in total, across 42 types).

### Gerakul.FastSql.Common

| Type | Members | Covered by |
|---|---|---|
| `ICommandCreator` | 1 | U8, I10 |
| `IWrappedCommand` | 18 | I3, I5 |
| `WrappedCommandExtensions` | 7 | I3 (`UseReaderAsync`), I9 (`WriteToServer` ×6) |
| `CommandCreatorExtensions` | 43 | U8 (commands), I3, I7, I8 (execution) |
| `CommandCompilator` | 46 | U7 |
| `SimpleCommand`, `MappedCommand<T>` | 2 + 2 | U7 |
| `CommandTextGenerator` | 12 | U1 |
| `ContextProvider` | 21 | U2, U9, I2 |
| `DbContext`, `ScopedContext`, `ConnectionStringContext`, `ConnectionContext`, `TransactionContext` | 7 + 2 + 9 + 6 + 2 | U9, I2, I3 |
| `QueryOptions`, `ReadOptions`, `BulkOptions` | 9 + 5 + 11 | U4, I3, I9 |
| `FieldSettings<T>`, `FieldSettings`, `FieldSettingsExtension` | 11 + 4 + 1 | U3 |
| `FromTypeOption`, `FieldsSelector` (enums) | – | U3, U5, I9 |
| `DataReaderExtensions` | 21 | U5 (reading), I9 (`WriteToServer` ×6) |
| `DbCommandExtensions` | 10 | I4 |
| `EnumerableExtensions` | 15 | U6 (`ToDataReader` ×8), I9 (`WriteToServer` ×6), U3 (`GetFieldSettings`) |
| `UniversalDataReader<T>` | 37 | U6 |
| `BulkCopy` (abstract, protected members) | 9 | Indirectly through I9 (not constructible without a custom provider) |

### Gerakul.FastSql.SqlServer

| Type | Members | Covered by |
|---|---|---|
| `SqlContextProvider` | 16 | U2, U9, I2 |
| `SqlConnectionStringContext`, `SqlConnectionContext`, `SqlTransactionContext` | 3 + 3 + 3 | U9, I2 |
| `ISqlCommandCreator` | – | U8, I7 |
| `CommandCreatorExtensions` (`CreateInsertAndGetID`) | 2 | U8, I7 |
| `SqlCommandTextGenerator` | 11 | U1 |
| `SqlQueryOptions` | 6 | U4 |
| `SqlBulkOptions` | 11 | U4, I9 |
| `ColumnDefinitionOptions` | 16 | U4, I9 |

### Gerakul.FastSql.PostgreSQL

| Type | Members | Covered by |
|---|---|---|
| `NpgsqlContextProvider` | 16 | U2, U9, I2 |
| `NpgsqlConnectionStringContext`, `NpgsqlConnectionContext`, `NpgsqlTransactionContext` | 3 + 3 + 3 | U9, I2 |
| `NpgsqlCommandTextGenerator` | 10 | U1 |
| `NpgsqlQueryOptions` | 6 | U4 |
| `NpgsqlBulkOptions` | 6 | U4, I9 |
| `ColumnDefinitionOptions` | 16 | U4 (unused by the provider, Q10) |
