# Parity notes (phase 5)

What phase 5 did, where it differs from the [parity plan](parity-plan.md), the behavior and public API changes
for the release notes, and the measurements. The plan was approved on 2026-11-20 with the recommended answers;
P4 (bulk copy with the column types) was left for later.

It came in four pull requests into `modernization`, each stacked on the one before:
gerakul/FastSql#29 (the plan and the probe results), #30 (bulk copy: P1, P5, P7), #31 (insert-and-get-ID: P2),
and #32 (`DateTimeOffset` and documentation: P3, P6).

## What was done

| Item | Result |
|---|---|
| P1 | A PostgreSQL bulk copy with `CreateTable` creates the table (Q10 fixed). The column types come from an Npgsql reader's column schema, with sizes added, or from the field types with a mapping that binary `COPY` fits. `NpgsqlBulkOptions` takes the PostgreSQL `ColumnDefinitionOptions`, and `CheckTableIfNotExistsBeforeCreation` becomes `create table if not exists`. |
| P2 | `CreateInsertAndGetID` is in Common, for both providers and every context, with no cast (the cast part of Q18). `CommandTextGenerator.InsertAndGetID` is virtual, and `CommandCompilator.CompileInsertAndGetID` has the five overloads of `CompileInsert`. |
| P3 | A `DateTime` of kind `Utc` reads into a `DateTimeOffset` member with offset 0, so PostgreSQL `timestamptz` does. PostgreSQL parameters and bulk copy send `DateTimeOffset` values in UTC (Q87 partly fixed). |
| P5 | `BulkCopyTimeout` moved from `SqlBulkOptions` to `BulkOptions`, and PostgreSQL sets `NpgsqlBinaryImporter.Timeout` from it. |
| P6 | README: table creation, the timeout and insert-and-get-ID on both providers, and a "Differences between the databases" section from D1 to D5. Also XML docs, the quirks register (Q10, Q18, Q87, Q104, new Q131), the coverage map, and these notes. |
| P7 | SQL Server `CreateTable` uses the reader's schema only when SqlClient made it, otherwise the field types, so a PostgreSQL query can create a SQL Server table (Q131). |

## Where it differs from the plan

1. **The PostgreSQL insert-and-get-ID statement.** This change came from the probes and is recorded in the
   plan's "Probe results". The plan picked `currval(pg_get_serial_sequence(...))`, but that fails, or returns a
   stale ID, when the insert gives the column a value itself. P2 uses
   `with inserted as (insert ... returning *)` and reads the inserted row's value of the column that the
   catalog lookup finds.
2. **A row with `DBNull` for a table without a generated key** (P2). The plan said "no row, so `ExecuteScalar`
   returns null, as on SQL Server". But SQL Server's `scope_identity()` gives a row with `DBNull`. With a
   `left join`, PostgreSQL now gives the same.
3. **The schema rule of `CreateTable`** (P1, P7). The plan's list of type names became a check of who made the
   schema: `NpgsqlDbColumn` for PostgreSQL, columns from the Microsoft.Data.SqlClient assembly for SQL Server.
   A list would have changed SQL Server readers of user-defined types (probe 1b). This is recorded in the plan.
4. **Unknown nullability is nullable** (P1). Npgsql reports `AllowDBNull` and `IsKey` only for readers opened
   with `KeyInfo`, which FastSql does not do. So a table created from a PostgreSQL query has nullable columns
   and no primary key, unless `ColumnDefinitionOptions.PrimaryKey` names one. This is recorded in the plan.
5. **No `InternalsVisibleTo`** (P1). The field-list check and its messages are shared through
   `shared/CreateTableFields.cs`, a file linked into both provider projects. The packages are not strong-named,
   and a provider package that used Common's internals would break when someone upgrades Common alone.
6. **`BulkOptions` takes the timeout in its constructor too** (P5). The plan mentioned only the property and the
   provider constructors. Provider-independent code can now write `new BulkOptions(bulkCopyTimeout: 60)`.

## Behavior changes for users

- PostgreSQL bulk copy with `CreateTable` creates the table; it threw `NotImplementedException`.
- SQL Server `CreateTable` from a reader of another database (anything but SqlClient's schema) declares the
  columns from the field types; it took that reader's type names, which failed for PostgreSQL ones.
- PostgreSQL: a `timestamptz` value reads into a `DateTimeOffset` member, with offset 0; this threw
  `InvalidCastException`. The same holds for any `DateTime` of kind `Utc` read into a `DateTimeOffset` member,
  on both providers.
- PostgreSQL: a `DateTimeOffset` with a non-zero offset, as a parameter or in a bulk copy, is stored as its
  instant. Before, Npgsql threw `ArgumentException`, and for a parameter it also closed the connection.
- PostgreSQL `ColumnDefinitionOptions` presets have a date and time scale of 6 instead of 7. The class was
  unused before this phase.

## Public API changes

All of them are additions, a member moved to a base class, or a constructor that gained parameters at its end.
Existing source code compiles and means the same. Compiled code needs a rebuild for the constructors that
changed, as for every 1.x upgrade.

- `BulkOptions`: `BulkCopyTimeout` (moved from `SqlBulkOptions`), and a `bulkCopyTimeout` constructor parameter.
- `NpgsqlBulkOptions`: a `ColumnDefinitionOptions` property; constructor parameters `createTable`,
  `columnDefinitionOptions`, `ignoreDataReaderSchemaTable`, `checkTableIfNotExistsBeforeCreation`,
  `bulkCopyTimeout`.
- `CommandTextGenerator.InsertAndGetID` (virtual). `SqlCommandTextGenerator` and `NpgsqlCommandTextGenerator`
  override it.
- `CommandCreatorExtensions.CreateInsertAndGetID<T>` (2 overloads, on `ICommandCreator`). The SQL Server ones on
  `ISqlCommandCreator` stay and forward to them.
- `CommandCompilator.CompileInsertAndGetID<T>` (5 overloads).

## Tests

At the end of the phase: 2196 unit tests and 2765 integration tests, all passing locally on the test images
(SQL Server 2022 CU27, PostgreSQL 18.6). New tests:

- PostgreSQL table creation: `PostgreSqlCreateTableTests` (45 tests) and `NpgsqlCreateTableTests` (the script).
- Insert-and-get-ID: `InsertAndGetIdTests` runs on both providers, plus PostgreSQL-only cases.
- Others: the timeout on both providers; cross-provider `CreateTable` in both directions; `DateTimeOffset` on
  every PostgreSQL read and write path; the conversion rule and the parameter in unit tests.

Tests that pinned the old behavior now assert the new behavior, tagged `Fixed`: Q10, Q87, and the cross-provider
`CreateTable` of Q131.

## Performance

None of the changes is on a measured hot path:

- P1 and P7 run once per copy, before the rows.
- P2 is new commands.
- P3 changes only the conversion that runs when a value does not fit its member, plus a type check per
  PostgreSQL parameter.

The PostgreSQL copy loop checks the `DateTimeOffset` columns once per copy. To confirm, the read benchmarks ran
back to back before and after P3 (BenchmarkDotNet, in-memory readers, 5 warm-up and 15 measured iterations):

| Benchmark | Before | After | Allocated (both) |
|---|---:|---:|---:|
| `ReadAll_Class`, 1 row | 390 ns | 390 ns | 640 B |
| `ReadAllFirstColumn_Int`, 1 row | 51 ns | 39 ns | 224 B |
| `ExecuteQuery_Class`, 1 row | 769 ns | 655 ns | 864 B |
| `ReadAll_Class`, 1,000 rows | 82.1 µs | 72.7 µs | 88,552 B |
| `ReadAllFirstColumn_Int`, 1,000 rows | 6.17 µs | 5.59 µs | 224 B |
| `ExecuteQuery_Class`, 1,000 rows | 75.7 µs | 73.5 µs | 88,776 B |

The "before" run was the noisier of the two: it ran while code was being edited. No benchmark became slower,
and allocations are the same.

## Left open

- P4: PostgreSQL bulk copy with the column types (Q85), as an opt-in option. Its type matrix is still to do.
- The differences D1 to D5, which the README now lists:
  - A PostgreSQL `CreateFunction`.
  - Identity handling like `KeepIdentity` in `COPY`.
  - `MERGE` as an opt-in.
- Q130: the field list of `CreateTable` is compared with case. It is still your decision, and it now applies to
  both providers.
- Package versions, and the 2.0.0 question, are unchanged; they are a release decision.
