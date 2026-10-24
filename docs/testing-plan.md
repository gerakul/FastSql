# Test plan (modernization phase 0)

This is the detailed plan for covering FastSql with unit and integration tests before the
modernization. The tools and the reasons behind them are in [testing-approach.md](testing-approach.md).
Phase 1 (.NET 10 and the package upgrade) is done; what it changed, in the code and in the tests, is in
[upgrade-notes.md](upgrade-notes.md). This plan describes the tests as they are after phase 1.

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
FastSql.slnx                                 the solution (FastSql.sln before phase 1)
Directory.Build.props                        settings of all projects (target framework, authors)
Directory.Packages.props                     every package version (central package management)
global.json                                  .NET 10 SDK; test runner = Microsoft Testing Platform
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

- Both test projects are added to the solution under a `tests` solution folder.
- `tests/Directory.Build.props` applies only to the test projects, so library builds are not
  affected. It imports the root `Directory.Build.props` (`net10.0`), sets `OutputType=Exe`,
  `IsPackable=false`, nullable reference types disabled (matching the library), and references the
  common packages: `xunit.v3`, `AwesomeAssertions`, `Microsoft.Testing.Extensions.CodeCoverage`
  (until phase 1 also `System.Interactive.Async` 3.2.0 with `Aliases="global,Ix"`, for the compat
  helper). It also links `tests/Shared/**/*.cs` into both projects.
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
- `TakeCompat(source, count, cancellationToken)`: enumerate partially, then dispose (for resource tests);
- `GetEnumeratorCompat(source)`: a small enumerator wrapper (`MoveNextAsync(token)`, `Current`,
  `Dispose()`) for tests about cancellation and disposal semantics;
- `ToAsyncEnumerableCompat(items)`: builds the library's async sequence type from in-memory items
  (input for `ToDataReader`), because the Ix and built-in `ToAsyncEnumerable` extensions clash by name.

After the upgrade only this file is rewritten (to `await foreach` / `GetAsyncEnumerator`).

Phase 1: the library returns the `IAsyncEnumerable<T>` built into .NET, which takes the token once, in
`GetAsyncEnumerator(token)`. So `GetEnumeratorCompat(source, token)` takes the token and the wrapper's
`MoveNextAsync()` has none (the call sites moved their token accordingly), and `ToAsyncEnumerableCompat`
uses `System.Linq.AsyncEnumerable`. The alias is gone.

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

Tests get one through `Spec.CreateCapturingContext()`, which returns an `ICapturingContext`
(`LastCommand`, `Commands`, `Context`). Without an argument it uses a new provider instance.

### 3.3 Provider specs (both projects)

A small `ProviderSpec` per provider (`SqlServerSpec`, `PostgreSqlSpec` in `tests/Shared`) holds what
differs between providers in tests:

- the default provider instance and a factory for new ones;
- creating provider connections;
- identifier quoting (`[Name]` / `"Name"`);
- creating parameters through `DbCommand.CreateParameter()` (name, value, direction).

Integration-specific operations (database creation, connection string changes, session counting,
scratch tables) live in the container and database fixtures (3.4, 3.5, 3.8). SQL snippets such as
"sleep N seconds" or "current isolation level" are added together with the tests that need them.

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
(found while building the infrastructure; relevant to I8, `UpgradeSensitive`). Since Npgsql 7
`CreateProcedure` runs `CALL`, which cannot call the function; tests that call `TestProc` go through
`DatabaseFixture.CallTestProc` (phase 1, see I8).

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

### Upgrade-sensitive behavior found in phase 0

The tests tagged `UpgradeSensitive` (`--filter-trait "UpgradeSensitive=..."` lists them by reason) pin these
driver and runtime behaviors. Expect them to change in phase 1, and review each change on its own
(what actually changed: [upgrade-notes.md](upgrade-notes.md#behavior-changes-found-by-the-tests)):

- **Npgsql 4 to 6+ / 7+:**
  - `timestamptz` is returned as UTC instead of local time, and writing a `DateTime` that is not UTC throws (Q87, `PostgreSqlTypeMappingTests`).
  - Stored procedures are called with `CALL` instead of selecting from a function (the whole `PostgreSqlStoredProcedureTests` class).
  - Binary `COPY` type handling (Q85, Q111, and the copies in `PostgreSqlBulkCopyTests`).
  - Async `CommandTimeout` (Q71).
  - Enum and json handling (Q86, Q89, the enum type pool test).
  - Parameter type inference (`NpgsqlContextProviderTests.AddWithValue_*`).
  - Exception types and messages: the `PostgresException` text, `OperationCanceledException` on cancel, `NpgsqlOperationInProgressException` (Q110).
- **System.Data.SqlClient to Microsoft.Data.SqlClient:**
  - `uniqueidentifier` reads should work again (Q80); remove the workarounds that avoid Guid columns.
  - `SqlBulkCopy` rounds instead of truncating since 2.0 (Q82).
  - Messages: "Execution Timeout Expired" instead of "Timeout expired", the closed connection messages (`Begin*` names for async methods), column mapping messages, the busy connection message (Q110).
  - `DbType` / `SqlDbType` inference (`SqlContextProviderTests.AddWithValue_*`).
  - `TaskCanceledException` on cancel.
  - The bulk load problem noted at the end of [§7](#7-quirks-register).
- **System.Interactive.Async to the `IAsyncEnumerable<T>` of .NET:** Q40 and Q42 depend on how Ix
  disposes and reports errors, and every async test goes through `AsyncEnumerableCompat`.
- **Unsigned CLR types** (I6): SQL Server Insert throws `ArgumentException` and bulk copy converts them; Npgsql throws on both paths.

## 5. Unit tests

Unit tests need no Docker. Classes marked *(per provider)* are abstract and run for both providers.

### U1. SQL text generation

Classes: `SqlCommandTextGeneratorTests`, `NpgsqlCommandTextGeneratorTests`.

- [x] `ColumnList`: one, several, none; quoting (`[x]` / `"x"`); quote characters inside names are not escaped (Q20).
- [x] `ParamList`, `WhereClause` (joined with `and`), `SetClause`.
- [x] `Insert`; `InsertWithOutput` (SQL Server `output inserted.[x]` before `values`, PostgreSQL `returning "x"`).
- [x] `InsertAndGetID` (SQL Server only): insert followed by `select scope_identity();`.
- [x] `Update(table, whereClause, fields)` and the base `Update(table, keyFields, fields)`.
- [x] `Delete(table, whereClause)` and the base `Delete(table, params keyFields)`, including the overload binding of `Delete("T", "ID")` (Q07).
- [x] `Merge`: single key, composite key, key repeated in fields (deduplicated), no non-key fields (Q09); SQL Server `MERGE` vs PostgreSQL `insert ... on conflict ... do update`; lines separated by `Environment.NewLine`.
- [x] Table names are used verbatim (schema-qualified, bracketed, quoted).

### U2. Provider hooks, through observable behavior

Classes: `SqlContextProviderTests`, `NpgsqlContextProviderTests` (capturing contexts).

- [x] Parameter discovery from command text (`CreateMapped` without explicit names): `@a`; repeated `@a` (one parameter); `@@ROWCOUNT` ignored; parameter at the start of the text and after a newline; names with digits and `$ # _`; `(@a,@b)`; `@` inside a string literal or an e-mail address is treated as a parameter (Q11).
- [x] Explicit parameter names with and without `@`: matching ignores the prefix, the parameter keeps the name as given.
- [x] Null values: SQL Server sends `SqlBytes.Null` for `byte[]` members and `DBNull` otherwise; PostgreSQL always `DBNull`.
- [x] Parameter `DbType` inferred by `AddWithValue` for common CLR types (string, int, long, decimal, bool, DateTime, DateTimeOffset, Guid, byte[], enum) (`UpgradeSensitive`).
- [x] Stored procedure parameters come from all settings, named `@` + member name.
- [x] `CommandTimeout` is set only when a timeout option has a value; otherwise the driver default stays.

### U3. Field settings

Class: `FieldSettingsTests`.

- [x] Each of the 5 public constructors: `Name`, `DataTypeName` (defaults to `FieldType.Name`), `FieldType`, `Getter`, `IsNullGetter`, `DetermineNullByValue`.
- [x] `FieldSettings<T>(name)`: identity getter for sequences of scalars.
- [x] `FieldSettings.FromType<T>(option)`: public fields first, then properties; `PublicField`, `PublicProperty`, `Default`/`Both`, `Collection`, `All`.
- [x] Collections (`List<int>`, `int[]`, `IEnumerable<string>`, dictionaries) are excluded unless `Collection` is set; `string` and `byte[]` are always included.
- [x] Static and non-public members excluded; inherited members included; an indexer property is included and its getter throws when invoked (Q21).
- [x] `FromType(proto)` (anonymous types), `GetFields<T>()`, `GetFields(proto)`, `GetNames()`, `IEnumerable<T>.GetFieldSettings()`.

### U4. Options

Classes: `QueryOptionsTests`, `ReadOptionsTests`, `BulkOptionsTests`, `SqlBulkOptionsTests`, `NpgsqlBulkOptionsTests`, `ColumnDefinitionOptionsTests` (both namespaces).

- [x] Every public constructor stores its arguments (including all 10 `SqlBulkOptions` arguments; `NpgsqlBulkOptions` leaves `CreateTable` and the other base options unset).
- [x] `Clone()` copies every property, returns the same runtime type, returns an independent instance; `SqlBulkOptions.Clone()` shares the `ColumnDefinitionOptions` instance (shallow).
- [x] Provider defaults: `QueryOptions`, `BulkOptions`, `ReadOptions` of both providers are the provider-specific types with all values unset.
- [x] Protected defaults (through test subclasses): `QueryOptions` (timeout unset, case-insensitive parameters); `BulkOptions` (`Source`, case sensitivity unset, no table creation, schema table used, no existence check); `SqlBulkOptions` (`SqlBulkCopyOptions.Default`, `ColumnDefinitionOptions.Default`).
- [x] `SetDefaults(other)` (through test subclasses) fills only unset values; `SqlBulkOptions` takes SQL Server values only from another `SqlBulkOptions` and clones its `ColumnDefinitionOptions`.
- [x] Protected copy constructors (through test subclasses): SQL Server / PostgreSQL options built from base options copy the base values; `SqlBulkOptions` copies the SQL Server values from another `SqlBulkOptions` and leaves them unset otherwise.
- [x] Timeout precedence on the generated command: none, provider level, context level (`ctx.QueryOptions.CommandTimeoutSeconds = n`), per command (wins); a base `QueryOptions` passed to a SQL Server context works.
- [x] Context creation clones the provider options: later changes on either side do not leak.
- [x] Case-sensitive parameter matching precedence: default (insensitive), context level, per command.
- [x] `ColumnDefinitionOptions`: `Default`, `Max`, `Small` values; `Clone()` copies the primary key list; `FromOption`, `FromDefault`, `FromMax`, `FromSmall` set the primary key and do not change the static source; `ThrowIfUnsupportedType` is false by default.

### U5. Mapping from data readers

Classes: `ReadAllTests`, `ReadAllAsyncTests`, `ReadAllAnonymousTests`, `ReadAllFirstColumnTests`, `ReadAllObjectsTests`, `ValueConversionTests`, `FieldsSelectorTests`.

Input comes from `DataTable.CreateDataReader()` and from a wrapper that implements only
`IDataReader` (to reach the non-`DbDataReader` async path).

- [x] `ReadAll<T>`: properties and public fields; `FromTypeOption` (fields only, properties only, `Collection` maps an array column); case-insensitive by default; case-sensitive option; column order does not matter.
- [x] `FieldsSelector` × (exact match, extra column, extra member, no overlap) for `Destination` (default), `Source`, `Common`, `Both`: rows mapped, or `InvalidOperationException` with the current message; an undefined selector value gives `ArgumentException`.
- [x] Duplicates: duplicate reader columns (case-insensitive and case-sensitive modes); a field and a property whose names differ only by case; `ArgumentException` with the current messages.
- [x] Nulls: `DBNull` becomes null for reference and `Nullable<T>` members; `DBNull` into a non-nullable value type throws `InvalidCastException` (Q12).
- [x] Conversions (regression set for commit 41af0f9): int→long, long→int, int→decimal, decimal→double, short→int?, string "42"→int, int→string, bool, Guid, byte[], DateTime, DateTimeOffset, TimeSpan; enum from int and from string (case-sensitive, invalid name throws `ArgumentException`); nullable enum from `DBNull`; Guid→string throws `InvalidCastException`.
- [x] Get-only property: required by the `Destination` selector, then fails when set (Q13).
- [x] Laziness: mapping errors appear when enumerating, not when calling `ReadAll`; enumerating the result a second time yields nothing (forward-only reader).
- [x] `ReadAllAnonymous`: matching by constructor parameter names; missing columns with `Common` give default values; `Destination` requires all parameters; case sensitivity; conversions; the first constructor is used for non-anonymous types.
- [x] `ReadAllFirstColumn()`: `DBNull` becomes null, while `ReadAll()` keeps `DBNull` in its arrays (Q12); `ReadAllFirstColumn<T>()`: conversion, nullable, enum.
- [x] `ReadAll()`: array length equals `FieldCount`.
- [x] `GetColumnNames()`.
- [x] Async twins of all of the above return the same results; the `IDataReader`-only path works; a cancelled token makes `MoveNext` return false instead of throwing (Q04); empty reader; disposing the enumerator.

### U6. UniversalDataReader and ToDataReader

Classes: `UniversalDataReaderTests`, `ToDataReaderTests`.

- [x] All 8 `ToDataReader` overloads (`IEnumerable` and legacy `IAsyncEnumerable`, × settings array / settings + case sensitivity / `FromTypeOption` / defaults): resulting `Settings`; case-sensitive by default.
- [x] Metadata: `FieldCount`, `GetName`, `GetOrdinal` (case-sensitive and insensitive; unknown name throws `KeyNotFoundException`), `GetFieldType`, `GetDataTypeName` (CLR type name or custom), `Depth` = 0.
- [x] Values: `Read` and `ReadAsync` over both source kinds; `GetValue`, `GetValues` (shorter and longer arrays), both indexers, `IsDBNull` (custom null predicate), typed getters (exact cast: `GetInt64` on an int throws `InvalidCastException`), `GetBytes` / `GetChars` (offsets, lengths, out-of-range returns 0).
- [x] Lifecycle: `Close()` on the reader sets `IsClosed` and `Read` then throws `InvalidOperationException`; `Close()` through a `DbDataReader` reference does not close it (Q01); `Dispose()` closes.
- [x] Not implemented: `NextResult`, `RecordsAffected`, `HasRows` throw `NotImplementedException` (Q02); `GetEnumerator()` returns the source enumerator, or null for an async source (Q03); `GetSchemaTable()` / `GetColumnSchema()` throw `NotSupportedException` (leads to Q14).
- [x] Duplicate names (in case-insensitive mode) throw `ArgumentException` at construction.
- [x] Round trip: `ToDataReader().ReadAll<T>()` returns equal objects (sync and async).

### U7. Command compilation *(per provider)*

Class: `CommandCompilatorTests`. Each compiled command is turned into a `DbCommand` through
`CreateSimple(precompiled)` / `CreateMapped(precompiled)` on a capturing context.

- [x] `CompileSimple`, `CompileProcedureSimple`: `CommandText`, `CommandType`.
- [x] `CompileMapped` (6 overloads): names from text vs explicit list; `FromTypeOption`; proto overloads; settings overloads; case-sensitive and insensitive matching; parameter without a setting throws `ArgumentException`; duplicate parameter names and duplicate settings throw `ArgumentException`; null parameter list throws `ArgumentNullException` (Q17; first expected as `NullReferenceException`).
- [x] `CompileProcedure` (6 overloads): stored procedure type; parameters from all settings or from an explicit subset.
- [x] `CompileInsert` (5 overloads): fields minus ignored fields; text from the generator; parameters match.
- [x] `CompileInsertWithOutput`: output fields; no output fields gives a plain insert; null ignore list.
- [x] `CompileUpdate` (10 overloads): keys in `where`, other fields in `set`, `notKeyIgnoreFields` excluded; a key missing from the settings leaves its parameter unmapped (pinned).
- [x] `CompileDelete` (5 overloads): explicit keys; no keys uses all members as keys.
- [x] `CompileMerge` (10 overloads).
- [x] Using a command compiled by one provider instance with a context of another provider instance throws `ArgumentException` (simple and mapped).
- [x] `new CommandCompilator(provider)` behaves like `provider.CommandCompilator`; `context.CommandCompilator` is the provider's.
- [x] Parameter values: null members become the provider's null; custom `IsNullGetter`; a member whose value is a `DbParameter` is added as is; simple commands name arguments `@p0`, `@p1`, ... by position (a `DbParameter` argument is added as is and still uses up its position), null becomes `DBNull`.

### U8. Command creator extensions *(per provider)*

Class: `CommandCreatorExtensionsTests`.

- [x] Each of the 43 `CommandCreatorExtensions` overloads in Common produces the expected command (text, type, parameters, timeout), and passes on its options (`queryOptions`; default `fromTypeOption` is `Default` for `CreateMapped` and `Both` for `CreateProcedure`).
- [x] `Insert`, `Update`, `Delete`, `Merge` and their async twins build the right command before executing (execution on an unopened connection throws `InvalidOperationException`; real execution is in I7).
- [x] `CreateInsertAndGetID` (2 overloads, SQL Server): command text and parameters.
- [x] Contexts returned by `CreateContext` are typed as the base classes, so `CreateInsertAndGetID` needs a cast; SQL Server contexts implement `ISqlCommandCreator` at runtime, PostgreSQL contexts have no equivalent.
- [x] Custom extensions through `ICommandCreator.Set(...)` (README "Extensibility") work on any context.
- [x] Scoped contexts build the command immediately and read member values at that moment (unit part of Q15).

### U9. Providers and contexts without a database

Class: `ContextProviderTests`.

- [x] `DefaultInstance` is a singleton; new instances are independent.
- [x] Provider members: `CommandTextGenerator`, `QueryOptions`, `BulkOptions`, `ReadOptions`, `CommandCompilator` (types and identity).
- [x] `CreateContext(string)` gives the provider's connection string context with `ConnectionString`; `CreateContext(DbConnection)` with an unopened connection gives the connection context; a connection of the other provider throws `InvalidCastException`.
- [x] Public constructors of all six context classes take the provider's options.
- [x] `DbContext.ContextProvider` and `DbContext.CommandCompilator`.

### U10. Public API snapshot

Class: `PublicApiTests`.

- [x] For each of the three assemblies, the API generated by `PublicApiGenerator` equals `PublicApi/Approved/<Assembly>.txt`. On a mismatch the test writes `<Assembly>.received.txt` next to it and fails with a hint to diff the two files.

## 6. Integration tests

Matrix shorthand: **P** = both providers, **K** = all three context kinds, **S/A** = sync and async.

### I1. Infrastructure smoke (P)

- [x] Container starts; per-class database is created; seed data is present (2 companies, 5 employees, `TestProc` callable).

### I2. Context lifecycle and transactions (P)

Class: `ContextLifecycleTests`.

- [x] `UsingConnection` / `UsingConnectionAsync`: the connection is open inside and closed afterwards; an exception propagates and the connection is still closed.
- [x] `UsingTransaction` / `UsingTransactionAsync`, with and without an isolation level, on connection string and connection contexts: commit on success; rollback and rethrow on exception; the isolation level reaches the server; `Unspecified` gives the server default.
- [x] Nested `UsingConnection` then `UsingTransaction`.
- [x] `CreateContext(DbTransaction)` and the transaction context constructors: commands run inside the transaction; a rollback by the caller discards them.
- [x] A connection context over a closed connection throws `InvalidOperationException` (the library does not open it).
- [x] Child contexts inherit the parent context's options, not the provider's.

### I3. Query execution (P × K × S/A)

Class: `WrappedCommandQueryTests`.

- [x] `ExecuteQuery<T>`, `ExecuteQuery()`, `ExecuteQueryAnonymous`, `ExecuteQueryFirstColumn()`, `ExecuteQueryFirstColumn<T>()`, `ExecuteScalar`, `ExecuteNonQuery` (rows affected) and their async twins, against the seed data.
- [x] Parameters: positional `@p0`, `@p1`; null argument; mapped from anonymous and named types; explicit names; settings; precompiled simple and mapped commands reused across contexts of the same provider.
- [x] `UseReader`, `UseReaderAsync` (both overloads): reader open inside, closed afterwards; multiple result sets reachable with `NextResult`; `ExecuteQuery*` reads only the first result set.
- [x] `ReadOptions` precedence: per call, context level, provider level.
- [x] Executing the same `IWrappedCommand` twice; member values read at execution (connection string context) vs at creation (scoped contexts) (Q15).
- [x] Empty result sets.
- [x] Server errors surface as `DbException`; the concrete exception type is recorded (`UpgradeSensitive`).

### I4. Raw DbCommand extensions (P × S/A)

Class: `DbCommandExtensionsTests`.

- [x] All 10 `DbCommandExtensions` methods on a provider `DbCommand`.
- [x] The reader is disposed after full enumeration; disposing early closes it, and the connection can run another command.

### I5. Execution semantics: laziness, resources, cancellation, timeouts (P)

Class: `ExecutionSemanticsTests`.

- [x] Laziness: query methods return without touching the database; invalid SQL fails only on enumeration.
- [x] No leaked sessions on connection string contexts after full enumeration, partial enumeration then dispose, an exception during enumeration, `UseReader`, bulk copy, and the async variants; exactly one extra session while an enumeration is open.
- [x] Cancellation: a cancelled token on `ExecuteQueryAsync`, `ExecuteScalarAsync` (scoped and connection string contexts); `ExecuteNonQueryAsync` on a connection string context and `UseReaderAsync` on scoped contexts ignore the token (Q06); `MoveNext` with a cancelled token returns false (Q04).
- [x] Command timeout: `QueryOptions(1)` with a 3 second server sleep fails with a `DbException` (detail `UpgradeSensitive`).

### I6. Type mapping (per provider, `UpgradeSensitive`)

Classes: `SqlServerTypeMappingTests`, `PostgreSqlTypeMappingTests`.

For each type (with a nullable twin): write through parameters (`Insert`) and through bulk copy,
read back through `ExecuteQuery<T>`, `ExecuteQueryAnonymous`, `ExecuteQueryFirstColumn<T>` and
`ExecuteQuery()` (recording the CLR type the driver returns).

- [x] SQL Server: bit, tinyint, smallint, int, bigint, decimal, numeric, money, float, real, date, time, datetime, datetime2, smalldatetime, datetimeoffset, uniqueidentifier, char, varchar, nchar, nvarchar, nvarchar(max), varbinary, varbinary(max), xml (as string), rowversion (as byte[]).
- [x] PostgreSQL: boolean, smallint, integer, bigint, numeric, real, double precision, date, time, timestamp, timestamptz (instant and `Kind` asserted separately), interval, uuid, char, varchar, text, bytea, json and jsonb (as string), int[] and text[] (with `FromTypeOption.Collection`), a PostgreSQL enum type read into a C# enum.
- [x] Enums stored as int and as string; enum values written as parameters.
- [x] Edge values: min/max, decimal precision and scale, unicode, empty string vs null, empty byte array, `DateTime.MinValue` into `datetime` (error pinned).

### I7. Data modification (P × K × S/A)

Classes: `InsertTests`, `InsertWithOutputTests`, `UpdateTests`, `DeleteTests`, `MergeTests`, `SqlServerInsertAndGetIdTests`.

- [x] `Insert`: all columns; ignored fields (identity); nulls; `queryOptions` overload; named and anonymous types; table name forms (`dbo.Employee`, `[Employee]`, `"Employee"`, `public."Employee"`); PostgreSQL table with lower-case columns and PascalCase members fails (Q19).
- [x] `CreateInsertWithOutput` (4 overloads): one and several output fields, read through `ExecuteQueryFirstColumn<int>` and `ExecuteQuery<T>`; ignored fields; no output fields gives a plain insert.
- [x] `CreateInsertAndGetID` (SQL Server): `ExecuteScalar` returns the identity as `decimal` (Q18); `ExecuteQueryFirstColumn<int>`.
- [x] `Update`: single and composite keys; rows affected; no matching row gives 0; nulls written.
- [x] `Delete`: by keys; by all members when no keys are given; the README shortcut `new { ID = 2 }`; a null key value matches nothing (Q08).
- [x] `Merge`: inserts when missing, updates when present, composite keys; PostgreSQL without a unique constraint on the keys fails (pinned); an object with only key members fails (Q09).
- [x] Each method's async twin.

### I8. Stored procedures and functions (P)

Class: `StoredProcedureTests`.

- [x] SQL Server: `TestProc` through `CreateProcedure`; an extra member fails with a server error (pinned); explicit parameter subset; settings overloads; `CreateProcedureSimple` with input, output and return value parameters (values read after execution); a `DbParameter` member used as an output parameter; a procedure with several result sets through `UseReader`.
- [x] PostgreSQL (`UpgradeSensitive`): a function returning a table through `CreateProcedure` (named notation, member order does not matter); `CreateProcedureSimple` with parameters from `DbCommand.CreateParameter()`; a void function through `ExecuteNonQuery`; calling a `procedure` fails with Npgsql 4 (pinned).
  - Phase 1: Npgsql 10 runs `CALL`, so it is the other way round: functions fail (the shared tests go through `DatabaseFixture.CallTestProc`, which pins the server's 42809 error and the arguments it shows) and procedures run, including one that returns its `INOUT` values as a row.

### I9. Bulk copy and data import (P × K × S/A)

Classes: `BulkCopyTests`, `SqlServerBulkCopyOptionsTests`, `PostgreSqlBulkCopyTests`, `CrossProviderImportTests`.

- [x] Every `WriteToServer` / `WriteToServerAsync` overload on `IEnumerable<T>` (6), `DbDataReader` (6) and `IWrappedCommand` (6), into all three destination context kinds; a transaction rollback discards the copied rows.
- [x] `fields` subset; all source columns by default; empty source.
- [x] Default `Source` selector maps by name; SQL Server matching is case-sensitive (Q16).
- [x] With `CaseSensitiveFieldsMatching` set (true or false) the destination columns are read and joined; `Destination`, `Common`, `Both` with mismatches throw `InvalidOperationException`.
- [x] Nulls; identity columns are generated.
- [x] `SqlBulkCopyOptions`: `KeepIdentity`, `KeepNulls` (column default), `CheckConstraints`, `FireTriggers`; `TableLock`, `BatchSize`, `BulkCopyTimeout`, `EnableStreaming` accepted (smoke).
- [x] SQL Server `CreateTable`: from a query reader (column types, lengths, nullability, checked in `INFORMATION_SCHEMA`; the source primary key is not reproduced, only `ColumnDefinitionOptions.PrimaryKey` creates one, Q103); from `IEnumerable<T>` only with `IgnoreDataReaderSchemaTable = true`, otherwise `NotSupportedException` (Q14); `ColumnDefinitionOptions` `Default` / `Max` / `Small` and a primary key list; an unsupported member type with `ThrowIfUnsupportedType` false and true; `CheckTableIfNotExistsBeforeCreation` with an existing table; an `xml` source column throws `NotSupportedException`.
- [x] PostgreSQL `CreateTable` throws `NotImplementedException` (sync and async) (Q10).
- [x] Cross-provider import: a SQL Server query written into a PostgreSQL table and the reverse, through `IWrappedCommand.WriteToServer`.

### I10. README and Examples scenarios (P)

Class: `ReadmeScenarioTests`.

- [x] Every README snippet, in order: simple example; context creation from a connection string, a connection and a transaction; `UsingConnection` / `UsingTransaction` and async variants; queries `data1` to `data11`; options; anonymous types; first column; non-query; insert and insert with output; update; merge; delete; bulk copy; stored procedures; data import; the extensibility examples (`CreateSelectTop10Employees`, with `limit 10` on PostgreSQL, and `GetTable<T>`). Snippets that change data run against scratch copies of `Employee` and `Company`. On PostgreSQL the bulk copy snippet and the import of a table into itself fail (Q111), and on connection and transaction contexts both import snippets fail (Q110).
- [x] The async lines keep the README syntax (`await ... .ToArray()`) on purpose: they change together with the README in phase 1.

## 7. Quirks register

Behavior that looks unintended, pinned by the tests as it is today. Q01 to Q21 were found while
reading the code and in the prototype, before the tests were written; the other IDs were found
by the implementation PRs (each PR had its own block of IDs, so some numbers are unused).

- Every item is pinned by tests tagged `[Trait("Quirk", "Qnn")]`; `git grep 'Trait("Quirk", "Qnn")' -- tests`
  lists them. The "Pinned in" column names the test classes (unit areas U*, integration areas I*).
- "confirmed" means reproduced by those tests on the pinned server images and drivers.
- Merged IDs were found twice; their tests carry the kept ID.
- Items that depend on the driver or runtime version also carry `UpgradeSensitive` (see
  [§4](#upgrade-sensitive-behavior-found-in-phase-0)).

### Found while planning (Q01 to Q21)

| ID | Behavior | Area | Status | Pinned in |
|---|---|---|---|---|
| Q01 | `UniversalDataReader.Close()` hides `DbDataReader.Close()` instead of overriding it: closing through a `DbDataReader` or `IDataReader` reference, or with the inherited `CloseAsync`, leaves the reader open. | U6 | confirmed | `UniversalDataReaderTests` |
| Q02 | `UniversalDataReader.NextResult`, `NextResultAsync`, `RecordsAffected`, `HasRows` throw `NotImplementedException`. | U6 | confirmed | `UniversalDataReaderTests` |
| Q03 | `UniversalDataReader.GetEnumerator()` returns the source `IEnumerator<T>` (rows are `T`, not records, and it shares the position with `Read`), or null for an async source. | U6 | confirmed | `UniversalDataReaderTests` |
| Q04 | The async enumerators return false from `MoveNext` when the token is cancelled, instead of throwing, and a mapping error is not raised. With the legacy enumerators a later `MoveNext` with a live token went on with the next row; the built-in enumerator (phase 1) gets its token once, so that cannot happen anymore. | U5, I5 | confirmed (second part gone in phase 1) | `ReadAllAsyncTests`, `ExecutionSemanticsTests` |
| Q05 | PostgreSQL `WriteToServerAsync` performs the copy synchronously: the returned task is already completed, and a cancelled token is ignored. | I9 | confirmed | `PostgreSqlBulkCopyTests` |
| Q06 | Cancellation token ignored by `ExecuteNonQueryAsync` on connection string contexts and by `UseReaderAsync` on scoped contexts. Connection string contexts also open the connection without the token for non-query, scalar, `UseReaderAsync` and bulk copy, but this is observable only for `ExecuteNonQueryAsync`: the others pass the token to the execute call anyway. | I5 | confirmed (partly observable) | `ExecutionSemanticsTests` |
| Q07 | `CommandTextGenerator.Delete("T", "ID")` binds to the `whereClause` overload and produces `delete from T where ID;`. | U1 | confirmed | `CommandTextGeneratorTests` |
| Q08 | `Delete` with a null key value compares with `= NULL` and deletes nothing. | I7 | confirmed | `DeleteTests` |
| Q09 | `Update` / `Merge` with no non-key members produce invalid SQL (an empty `set` clause), which the server rejects. | U1, I7 | confirmed | `SqlCommandTextGeneratorTests`, `NpgsqlCommandTextGeneratorTests`, `UpdateTests`, `MergeTests` |
| Q10 | PostgreSQL bulk copy with `CreateTable` throws `NotImplementedException` ("Table creation is not implemented for PostgreSQL"), sync and async, however the option is set; the public PostgreSQL `ColumnDefinitionOptions` is an unused copy of the SQL Server one. | U4, I9 | confirmed | `ColumnDefinitionOptionsTests` (PostgreSQL), `NpgsqlBulkOptionsTests`, `PostgreSqlBulkCopyTests` |
| Q11 | Parameter discovery treats `@` inside string literals, comments and e-mail addresses as parameters. | U2 | confirmed | `ProviderHooksTests` |
| Q12 | `DBNull` into a non-nullable value type throws `InvalidCastException`; `ReadAll()` keeps `DBNull` while `ReadAllFirstColumn()` returns null. See Q50 for enums and Q112 for `UniversalDataReader`. | U5 | confirmed | `ReadAll*Tests`, `ValueConversionTests` |
| Q13 | Get-only properties count as destination members when reading, then fail when set ("Property set method not found."). | U5 | confirmed | `ReadAllTests`, `ReadAllAsyncTests` |
| Q14 | SQL Server bulk copy of `IEnumerable<T>` with `CreateTable` throws `NotSupportedException` unless `IgnoreDataReaderSchemaTable` is true, because `UniversalDataReader.GetSchemaTable()` / `GetColumnSchema()` throw it (while `CanGetColumnSchema()` returns true). | U6, I9 | confirmed | `UniversalDataReaderTests`, `SqlServerBulkCopyOptionsTests` |
| Q15 | Member values are read when the command is executed on connection string contexts, but when it is created on scoped contexts; scoped contexts reuse one `DbCommand` for repeated executions. See also Q92. | U8, I3 | confirmed | `CommandCreatorExtensionsTests`, `WrappedCommandQueryTests` |
| Q16 | SQL Server bulk copy with the default `Source` selector maps columns case-sensitively (`name` does not match `Name`). | I9 | confirmed | `SqlServerBulkCopyOptionsTests` |
| Q17 | Compiling a mapped command with a null parameter list throws `ArgumentNullException` (parameter `source`, from LINQ) instead of a clear error. The plan first expected `NullReferenceException`. | U7 | confirmed (corrected) | `CommandCompilatorTests` |
| Q18 | `CreateInsertAndGetID(...).ExecuteScalar()` returns the identity as `decimal`; `CreateContext` returns base types, so the method needs a cast to `ISqlCommandCreator`. | U8, I7 | confirmed | `SqlCommandCreatorTests`, `SqlServerInsertAndGetIdTests` |
| Q19 | PostgreSQL quotes column names but not table names, in the text generator and in bulk copy (`COPY`), so members must match column case exactly while table names need manual quoting. | U1, I7, I9 | confirmed | `NpgsqlCommandTextGeneratorTests`, `PostgreSqlInsertTests`, `PostgreSqlBulkCopyTests` |
| Q20 | Identifier quoting does not escape quote characters inside names. | U1 | confirmed | `SqlCommandTextGeneratorTests`, `NpgsqlCommandTextGeneratorTests` |
| Q21 | Indexer properties are included by `FieldSettings.FromType` and their getters throw when invoked (`TargetParameterCountException`). See Q53 for reading. | U3 | confirmed | `FieldSettingsTests` |

### Found while implementing (Q30 and later)

| ID | Behavior | Area | Status | Pinned in |
|---|---|---|---|---|
| Q30 | The text generators quote column names but use the field name as the parameter name, so a name that needs quoting gives invalid SQL (`[First Name] = @First Name`). | U1 | confirmed | `CommandTextGeneratorTests` |
| Q31 | A `ReadOptions` instance passed directly to the reading methods on a reader, or to the raw `DbCommandExtensions.ExecuteQuery*`, is filled with the defaults in place when enumeration starts. Contexts clone the options first, so `IWrappedCommand` callers are not affected. | U4, U5, I4 | confirmed | `ReadOptionsTests`, `ReadAll*Tests`, `DbCommandExtensionsTests` |
| Q32 | An empty explicit parameter name throws `IndexOutOfRangeException` instead of an `ArgumentException`. | U2 | confirmed | `ProviderHooksTests` |
| Q33 | Parameter discovery from the text is case-sensitive while matching is case-insensitive by default, so one parameter spelled in two cases (`@ID`, `@id`) throws "Parameter names has been duplicated"; with case-sensitive matching it gives two parameters. | U2, U7 | confirmed | `ProviderHooksTests`, `CommandCompilatorTests` |
| Q40 | `UniversalDataReader` never disposes the source enumerator (`Close` and `Dispose` don't), for both source kinds. | U6 | confirmed | `UniversalDataReaderTests` |
| Q41 | `UniversalDataReader.ReadAsync` ignores its token: with a cancelled token it still reads the next row. | U6 | confirmed | `UniversalDataReaderTests` |
| Q42 | `UniversalDataReader.Read()` over a legacy async source blocks on the task, so a source exception surfaces wrapped in `AggregateException`; `ReadAsync` and enumerable sources throw the original exception. | U6 | confirmed | `UniversalDataReaderTests` |
| Q43 | `UniversalDataReader.GetBytes` / `GetChars` with a null buffer throw `NullReferenceException` instead of returning the length; `GetChars` works only for `char[]` values, a `string` value throws `InvalidCastException`. | U6 | confirmed | `UniversalDataReaderTests` |
| Q44 | `UniversalDataReader` value getters never check the reader state: before the first `Read` they run on `default(T)`, after the end they return the last row, after `Close` they keep working. | U6 | confirmed | `UniversalDataReaderTests` |
| Q45 | Collection detection looks only at the implemented interfaces: a member typed as the non-generic `IEnumerable` counts as a scalar, and any type implementing `IEnumerable<byte>` (`List<byte>`) counts as a scalar like `byte[]`. Used by `FromType` and by reading. | U3, U5 | confirmed | `FieldSettingsTests` |
| Q46 | Write-only properties are included by `FieldSettings.FromType`; their getter throws `ArgumentException` ("Property Get method was not found."). | U3 | confirmed | `FieldSettingsTests` |
| Q47 | `UniversalDataReader.GetValue` returns null (not `DBNull.Value`) for a null member while `IsDBNull` is true, and with by-value null detection a getter returning `DBNull.Value` is not `IsDBNull`. Consequence: Q112. | U6 | confirmed | `UniversalDataReaderTests` |
| Q48 | A member hidden with `new` and another type appears twice in `FieldSettings.FromType`, so `ToDataReader()` throws `ArgumentException` for the duplicate name even when case-sensitive. | U3, U6 | confirmed | `FieldSettingsTests`, `ToDataReaderTests` |
| Q50 | `DBNull` into a non-nullable enum throws `ArgumentException` from `Enum.ToObject`, not the `InvalidCastException` of other value types (Q12). | U5 | confirmed | `ValueConversionTests` |
| Q51 | Merged into Q31 (the same behavior, found again). | U5 | merged into Q31 | – |
| Q52 | A value that does not implement `IConvertible` (`Guid`, `byte[]`, `DateTimeOffset`, `TimeSpan`, arrays) maps only into a member of exactly its type; an `object` or interface member throws `InvalidCastException` "Object must implement IConvertible.". Introduced by the values casting fix 41af0f9 (before it, such values were assigned directly). See also Q87. Since phase 1, `DateOnly` and `TimeOnly` (what Npgsql 10 returns for `date` and `time`) are converted for `DateTime` and `TimeSpan` members; other members follow this rule. | U5 | confirmed | `ValueConversionTests`, `ReadAllTests`, `ReadAllAsyncTests`, `PostgreSqlTypeMappingTests` |
| Q53 | `ReadAll<T>` counts an indexer as a destination member named `Item`: the default `Destination` selector then needs an `Item` column, and with one, setting it throws `TargetParameterCountException`. | U5 | confirmed | `ReadAllTests`, `ReadAllAsyncTests` |
| Q54 | `ReadAllAnonymous` with a type that has no public constructor throws `InvalidOperationException` "Sequence contains no elements" on enumeration. | U5 | confirmed | `ReadAllAnonymousTests` |
| Q60 | `CreateSimple(text, null)` binds the `null` to the `params` array itself, so building the command throws `NullReferenceException`; `(object)null` gives a `DBNull` parameter. | U8 | confirmed | `CommandCreatorExtensionsTests` |
| Q61 | `CreateInsertAndGetID` casts the creator to `DbContext`, so an `ISqlCommandCreator` that is not a context throws `InvalidCastException`. | U8 | confirmed | `SqlCommandCreatorTests` |
| Q70 | The async mapped queries (`ExecuteQueryAsync<T>`, `ExecuteQueryAnonymousAsync`) open the reader before building the mapping and leak it when building fails (for example a missing column with the default selector). On scoped contexts and raw commands the next command on the connection then fails, and on SQL Server a transaction can be neither committed nor rolled back. Sync methods and connection string contexts clean up. | I3, I4, I5 | confirmed | `DbCommandExtensionsTests`, `ExecutionSemanticsTests` |
| Q71 | PostgreSQL: Npgsql 4 does not apply `CommandTimeout` to async execution, so a timeout shorter than the server's work fails the sync methods but not `ExecuteNonQueryAsync`, `ExecuteScalarAsync` or `ExecuteQueryAsync`. | I5 | gone with Npgsql 10 (phase 1): the async methods time out too | `PostgreSqlExecutionSemanticsTests` |
| Q80 | SQL Server: System.Data.SqlClient 4.5.1 cannot read `uniqueidentifier` values on .NET 9 and later (`TypeLoadException` for `SqlGuidCaster`). On the mapping paths the reader is left half read, and disposing it waits for the command timeout (`SqlException` "Timeout expired"). Bulk copy from such a query fails the same way. Writing Guids works. A driver and runtime issue, not library code. | I6, I9 | gone with Microsoft.Data.SqlClient (phase 1): `uniqueidentifier` is a regular round-trip case now | `SqlServerTypeMappingTests`, `SqlServerBulkCopyOptionsTests` |
| Q81 | SQL Server: `DateTime` parameters are sent as `datetime`, so Insert into `datetime2` loses precision and dates before 1753 fail with `SqlTypeException` even for `date` / `datetime2` columns; bulk copy keeps the full precision and range. | I6 | confirmed | `SqlServerTypeMappingTests` |
| Q82 | SQL Server: bulk copy truncates extra fractional digits (decimal, datetimeoffset) while Insert rounds on the server. | I6 | confirmed; decimal part gone with Microsoft.Data.SqlClient (phase 1), which rounds it | `SqlServerTypeMappingTests` |
| Q83 | SQL Server: a C# enum written into a string column stores the number through Insert and the name through bulk copy. | I6 | confirmed | `SqlServerTypeMappingTests` |
| Q84 | SQL Server: a `DateTime` written into `datetimeoffset` gets offset +00:00 through Insert but the client machine's local offset through bulk copy. | I6 | confirmed | `SqlServerTypeMappingTests` |
| Q85 | PostgreSQL: binary `COPY` writes each value with the type Npgsql infers from its CLR type, not with the column type, so `int` into `bigint`, `DateTime` into `date` (even when read from a `date` column) and similar combinations fail on the server; Insert lets the server cast the same values. | I6, I9 | confirmed; since Npgsql 10 (phase 1) `date` and `time` read as `DateOnly` / `TimeOnly`, which COPY writes correctly, so copying them from a query works | `PostgreSqlTypeMappingTests`, `PostgreSqlBulkCopyTests` |
| Q86 | PostgreSQL: string parameters are `text`, so Insert into `json`, `jsonb` and enum-type columns fails; bulk copy works for `json` and enum types but fails for `jsonb`. | I6 | confirmed | `PostgreSqlTypeMappingTests` |
| Q87 | `DateTime` and `DateTimeOffset` do not convert into each other when reading. On PostgreSQL this hits every `timestamptz` column (Npgsql 4 returns a local `DateTime`, Npgsql 10 a UTC one); `timestamptz` also stores any `DateTime` as wall clock, and Npgsql 10 rejects a `DateTimeOffset` with a non-zero offset. | I6 | confirmed (changed with Npgsql 10, phase 1) | `PostgreSqlTypeMappingTests`, `SqlServerTypeMappingTests` |
| Q88 | Array members are skipped silently with the default `FromTypeOption`: Insert and enumerable bulk copy leave the column NULL, `ExecuteQuery<T>` leaves the member null; `FromTypeOption.Collection` is needed. | I6 | confirmed | `PostgreSqlTypeMappingTests` |
| Q89 | PostgreSQL: Npgsql cannot write a C# enum at all (Insert or bulk copy, into enum-type, integer or text columns) unless the enum is registered with Npgsql (`NotSupportedException` with Npgsql 4, `InvalidCastException` with Npgsql 10). | I6 | confirmed | `PostgreSqlTypeMappingTests` |
| Q90 | The ignore lists of `Insert`, `CreateInsertWithOutput` and `CreateInsertAndGetID` are compared case-sensitively, although parameter matching is case-insensitive by default: `Insert(t, value, "id")` does not ignore `ID`. | I7 | confirmed | `InsertTests` |
| Q91 | PostgreSQL `Merge` (`insert ... on conflict ... do update`) checks the NOT NULL constraints of the proposed insert row first, so an object that leaves out a NOT NULL column fails even when the row exists; SQL Server `MERGE` updates the given columns. | I7 | confirmed | `PostgreSqlMergeTests`, `SqlServerMergeTests` |
| Q92 | Connection string contexts rebuild the command for every execution and add the same caller-supplied `DbParameter` objects again, so the second execution of such an `IWrappedCommand` throws; scoped contexts reuse their command. See Q15. | I3, I8 | confirmed | `StoredProcedureTests` |
| Q100 | SQL Server `CreateTable` with an unsupported member type and `ThrowIfUnsupportedType` false (the default) throws `NullReferenceException`, even when `fields` excludes that member. | I9 | confirmed | `SqlServerBulkCopyOptionsTests` |
| Q101 | Merged into Q85 (the same behavior, found again). | I9 | merged into Q85 | – |
| Q102 | Merged into Q80 (the same behavior, found again). | I9 | merged into Q80 | – |
| Q103 | SQL Server `CreateTable` from a query reader does not reproduce the source primary key (the reader reports no key information); only `ColumnDefinitionOptions.PrimaryKey` creates one. | I9 | confirmed | `SqlServerBulkCopyOptionsTests` |
| Q104 | SQL Server `CreateTable` with `IgnoreDataReaderSchemaTable` over a database reader creates value-type columns as `not null`, because the reader reports `int`, not `int?`, so a null value then fails. | I9 | confirmed | `SqlServerBulkCopyOptionsTests` |
| Q110 | A data import whose source and destination are the same scoped context fails: the source reader keeps the connection busy while the bulk copy needs it (SQL Server `InvalidOperationException`; PostgreSQL `NpgsqlOperationInProgressException` with Npgsql 4, `InvalidOperationException` "Connection is busy" with Npgsql 10). Connection string contexts use a connection each and work. The README import examples hit this with connection and transaction contexts (on PostgreSQL only the query import since Npgsql 10: the TestProc import fails earlier, see I8). | I9, I10 | confirmed | `ReadmeScenarioTests` |
| Q111 | PostgreSQL bulk copy writes the identity (`serial`) values of the source, unlike SQL Server, which generates them unless `KeepIdentity` is set. So the README bulk copy example (new objects with `ID = 0`) and the README import of a table into itself fail with a duplicate key (23505) on PostgreSQL. | I9, I10 | confirmed | `PostgreSqlBulkCopyTests`, `PostgreSqlReadmeScenarioTests` |
| Q112 | Reading a `UniversalDataReader` (`ToDataReader()`) null value into a non-nullable value-type member gives the default value (0) instead of the `InvalidCastException` of database readers (Q12), because `GetValue` returns null (Q47); `ReadAllFirstColumn<int>()` throws `NullReferenceException` instead. | U5, U6 | confirmed | `ToDataReaderTests` |

Observed but not pinned: a failed async SQL Server bulk copy (a client-side null check failing in the
middle of the copy) once returned its pooled connection in the middle of the bulk load, and the next
command on that connection failed with "Bulk load data was expected but not sent. The batch will be
terminated." It did not reproduce in isolation (timing dependent), so the affected test uses an
unpooled connection. Worth checking again after the move to Microsoft.Data.SqlClient.

## 8. CI

Workflow `.github/workflows/tests.yml`:

- Triggers: pull requests into `modernization` and `master`, pushes to them, manual run.
- Concurrency group per branch, cancelling superseded runs.
- Jobs on `ubuntu-latest` with .NET 10 (`actions/setup-dotnet`) and a NuGet cache:
  - **build**: builds the whole solution (`FastSql.slnx`, so build breaks in Examples are caught too).
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

The plan's first estimate was about 450 to 550 unit test cases and 700 to 1,000 integration test
cases (counting theory rows and the provider / context kind / sync-async combinations), with unit
tests under 15 seconds and integration tests a few minutes in CI including image pulls.

### Results after PR 10

Test cases as reported by the runner, and run times in CI (GitHub Actions, `ubuntu-latest`):

| Project | Test cases | Test run | Job, including restore and build |
|---|---|---|---|
| Unit | 1,747 | about 2 s | about 40 s |
| Integration | 2,499 | about 70 s, including the container starts | about 1 min 45 s |

After phase 1: 1,783 unit and 2,519 integration test cases (the changes are listed in
[upgrade-notes.md](upgrade-notes.md)).

Coverage of the `netstandard2.0` build, unit and integration results merged (a line counts as
covered when either project runs it; branch numbers take the better project per line, so they are
approximate):

| Assembly | Lines covered | Line rate | Branches covered | Branch rate |
|---|---|---|---|---|
| Gerakul.FastSql.Common | 1,048 / 1,055 | 99.3% | 444 / 450 | 98.7% |
| Gerakul.FastSql.SqlServer | 367 / 375 | 97.9% | 254 / 269 | 94.4% |
| Gerakul.FastSql.PostgreSQL | 190 / 307 | 61.9% | 97 / 237 | 40.9% |

Every public and protected member with a body is executed. The remaining uncovered code is
internal or private, and none of it is reachable in a way a test could pin:

| Code | Uncovered | Why |
|---|---|---|
| PostgreSQL `ColumnDefinition`, the internal `DataReaderExtensions.GetColumnDefinitions` and `EnumerableExtensions` helpers | 112 lines | Dead: a copy of the SQL Server table creation code. It would run only behind `CreateTable`, which throws `NotImplementedException` first (Q10). |
| PostgreSQL `NpgsqlBulkCopy.Transaction` and `NpgsqlBulkOptions` properties | 4 lines | Dead: never used (the binary import does not take a transaction). |
| `NpgsqlBulkCopy.Connection` and `MsSqlBulkCopy.Connection` fallback (`return null`) | 1 + 1 lines | Unreachable: a bulk copy is only created for connection and transaction contexts. |
| SQL Server `ColumnDefinition.GetColumnDefinitions(IEnumerable<FieldSettings<T>>)` and both `CreateTableScript<T>` overloads, the internal `EnumerableExtensions` script helpers | 7 lines | Dead: "temporary internal" helpers that nothing calls. |
| `DataReaderAttachedToConnectionStringContext.Context` and `DataReaderAttachedToScopedContext.Context` | 2 lines | Dead: the internal `IBulkWriter.Context` property is never read. |
| `DataReaderExtensions.GetBulkWriter`: "Unknown type of context" | 1 line | Unreachable: every context is a `ScopedContext` or a `ConnectionStringContext`, and `DbContext` cannot be subclassed outside the library (it has an internal abstract member). |
| `ReadInfo<T>.Dispose` | 3 lines | Dead: never called; the readers are disposed directly. |
| `ReadInfoFactory.CreateAnonymous`: case-sensitive duplicate constructor parameter names | 1 line | Unreachable from C#, which does not allow duplicate parameter names. |
| Branches: the `numeric` case of SQL Server `ColumnDefinition.GetTypeDeclaration` and the compiler-generated branches of that string switch; `options ?? ColumnDefinitionOptions.Default` in `FromFieldType`; the null-option and `IsKey ??` branches of the SQL Server `GetColumnDefinitions`; one null-conditional combination in the async enumerators' dispose (`state?.ReadInfo?.Reader?.Dispose()`) | the other partly covered branches | Unreachable: SqlClient reports `numeric` columns as `decimal`, the options are never null there (the defaults fill them), the reader always reports `IsKey`, and the enumerator state is never null when it is disposed. |

PR 10 closed the gaps that were reachable:
- null values from `UniversalDataReader` into non-nullable members (Q112);
- a property hiding an inherited field of the same name;
- the `ContextProvider` extension points `ApplyQueryOptions(cmd, null)` and `GetTyped` with `needClone`;
- more SQL Server `CreateTable` column types and nullable member types.

Done criteria:

- [x] All checklist items in this plan are ticked.
- [ ] CI is green on `modernization` (for you to confirm after merging PR 10; CI on the PR 10 branch is green).
- [ ] You have reviewed the quirks register ([§7](#7-quirks-register)).

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

Checked against the coverage results in PR 10 ([§9](#9-coverage-and-definition-of-done)): every public and
protected member with a body is executed by at least one test, and all uncovered lines are in internal or
private code. The "Covered by" column lists the areas that test each type on purpose; the README scenarios
(I10) also go through most of the Common types.

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
| `ContextProvider` | 21 | U2 (including the protected extension points `ApplyQueryOptions` and `GetTyped`, through a provider subclass), U9, I2 |
| `DbContext`, `ScopedContext`, `ConnectionStringContext`, `ConnectionContext`, `TransactionContext` | 7 + 2 + 9 + 6 + 2 | U9, I2, I3, I10 |
| `QueryOptions`, `ReadOptions`, `BulkOptions` | 9 + 5 + 11 | U4, I3, I9 |
| `FieldSettings<T>`, `FieldSettings`, `FieldSettingsExtension` | 11 + 4 + 1 | U3 |
| `FromTypeOption`, `FieldsSelector` (enums) | – | U3, U5, I9 |
| `DataReaderExtensions` | 21 | U5 (reading), I9 (`WriteToServer` ×6) |
| `DbCommandExtensions` | 10 | I4 |
| `EnumerableExtensions` | 15 | U6 (`ToDataReader` ×8), I9 (`WriteToServer` ×6), U3 (`GetFieldSettings`) |
| `UniversalDataReader<T>` | 37 | U6 (and U5 through `ToDataReader().ReadAll*`, Q112) |
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
| `ColumnDefinitionOptions` | 16 | U4 (unused by the provider, Q10; the internal table creation code behind it is dead, see §9) |
