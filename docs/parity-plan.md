# Parity plan (phase 5)

Status: approved on 2026-11-20 as recommended (see [Decisions needed](#decisions-needed) for the answers),
after the probes below were planned; their results are in [Probe results](#probe-results).

Goal of this phase: every contract of FastSql works on both SQL Server and PostgreSQL wherever the two
databases allow it. Where they cannot behave the same, the difference is written down.

Rules for this phase (the phase 4 rules, unless you say otherwise):

- The overall design stays: context providers, the three context kinds, `ICommandCreator` /
  `IWrappedCommand`, compiled commands, `FieldSettings`, `UniversalDataReader`, bulk copy.
- Public contract changes are small, and each one is listed in [Public API](#public-api). Users of 1.x
  recompile for the next release anyway.
- Every behavior change is listed and goes into the release notes.
- No performance regression. The benchmarks run before and after each PR that touches a hot path.

## Answer: no blockers

**Nothing blocks this goal.** Every contract that Common declares can be implemented on PostgreSQL.
A few things cannot be made identical, because the two databases differ. None of them stops a contract
from working; they are listed in [Differences that stay](#differences-that-stay).

What I compared: the approved public API files of the three packages, both providers' source, the quirks
register in `docs/testing-plan.md`, the "Later" list of the phase 4 plan, and the tests that pin
PostgreSQL behavior. I also checked the Npgsql 10.0.3 package for the APIs the plan needs
(`NpgsqlBinaryImporter.Timeout`, `Write<T>(value, NpgsqlDbType)`, the `Npgsql.EnableStoredProcedureCompatMode`
switch).

## What is already the same

- Every abstract member of `ContextProvider`, `CommandTextGenerator` and `BulkCopy`, and both options
  hierarchies (`QueryOptions`, `BulkOptions`), is implemented by both providers.
- All the `CommandCreatorExtensions` methods in Common, all of `IWrappedCommand`, the three context kinds,
  transactions, `ReadOptions`, and the mapping in both directions.
- Bulk copy from a query, a reader or an `IEnumerable<T>`, with `FieldsSelector` and case matching.
- `Merge`, `InsertWithOutput`, `Update` and `Delete` produce the right statement for each database.
- `CreateProcedure` / `CreateProcedureSimple` run on both. On PostgreSQL they call procedures, not
  functions (see D1 below).

## The gaps

| # | Contract | SQL Server | PostgreSQL today | Register |
|---|---|---|---|---|
| G1 | `BulkOptions.CreateTable`, with `IgnoreDataReaderSchemaTable` and `CheckTableIfNotExistsBeforeCreation` | works | throws `NotImplementedException`. The public `ColumnDefinitionOptions` is unused, and `NpgsqlBulkOptions` cannot take it | Q10 |
| G2 | `CreateInsertAndGetID` (and `InsertAndGetID` of the text generator) | works, but only on `ISqlCommandCreator`, so a context from `CreateContext` needs a cast | missing | Q18 |
| G3 | Reading a `DateTimeOffset` member | works from `datetimeoffset` | fails from `timestamptz` (`InvalidCastException`). Writing a value with a non-zero offset fails too (Npgsql refuses it) | Q87 |
| G4 | Bulk copy into a column of a wider or different type | `SqlBulkCopy` converts | binary `COPY` writes the CLR type, so `int` into `bigint`, `DateTime` into `date`, `string` into `jsonb` fail | Q85 |
| G5 | Bulk copy timeout | `SqlBulkOptions.BulkCopyTimeout` | no option, although `NpgsqlBinaryImporter.Timeout` exists | – |

There is also a gap on the SQL Server side. `CreateTable` takes column type names from the source
reader's schema, even when the reader belongs to another database. A PostgreSQL query copied into a new
SQL Server table therefore fails with "Cannot find data type character varying" (pinned in
`CrossProviderImportTests`). P1 has to pick a rule for PostgreSQL's mirror case, and P7 applies the same
rule to SQL Server.

## Differences that stay

These are where the databases themselves differ. The plan documents them and does not try to hide them.

- **D1. Routines that return rows.** A SQL Server procedure returns result sets. A PostgreSQL procedure
  cannot; only a function can. Since Npgsql 7, `CommandType.StoredProcedure` runs `CALL`, so
  `CreateProcedure` reaches PostgreSQL procedures (and their INOUT values) but not functions. A shared
  "call a function" contract does not work either: SQL Server table-valued functions take positional
  arguments only, so members cannot be matched to parameters by name. Users who need functions can turn on
  Npgsql's `Npgsql.EnableStoredProcedureCompatMode` switch (process-wide; procedures then stop working) or
  use `CreateSimple("select * from f(@a, @b)")`. A PostgreSQL-only `CreateFunction` is a possible later
  feature.
- **D2. `timestamptz` stores an instant, not an offset.** A `DateTimeOffset` comes back from PostgreSQL
  with offset 0, while SQL Server's `datetimeoffset` keeps the offset. P3 makes such values work, but it
  cannot keep the offset.
- **D3. `COPY` has no counterpart for most `SqlBulkCopy` settings.** `BatchSize`, `EnableStreaming` and
  `SqlBulkCopyOptions` (`KeepIdentity`, `KeepNulls`, `CheckConstraints`, `FireTriggers`, `TableLock`,
  `UseInternalTransaction`) have nothing to map to. `COPY` always checks constraints, fires triggers, keeps
  nulls and streams the rows. The timeout is the one exception (P5). Q111 also belongs here: PostgreSQL
  writes the source's identity values, while SQL Server generates new ones unless `KeepIdentity` is set.
  Matching SQL Server would mean reading the table's identity columns on every copy (an extra round trip
  on the default path), and it would change what PostgreSQL copies do today. Not proposed.
- **D4. Upsert semantics.** PostgreSQL `Merge` is `insert ... on conflict`. It needs a unique constraint
  on exactly the key columns, and it checks the NOT NULL columns of the row it would insert (Q91).
  PostgreSQL's own `MERGE` statement would remove both differences. PostgreSQL 14 reached end of life on
  2026-11-12, so every supported version (15 to 18) has `MERGE`. But it would give up `on conflict`'s
  guarantee under concurrent upserts, which existing users may rely on. Not proposed; it could be an
  opt-in later.
- **D5. Type system details.** PostgreSQL has no `tinyint` (a `byte` becomes `smallint`), `bytea` has no
  length, timestamps allow at most 6 fractional digits (SQL Server allows 7), string parameters are
  `text` (Q86), and C# enums need registering with Npgsql (Q89). The plan works with these and does not
  change them.

## Plan items

| ID | Change | Closes | Behavior change | Public API | Rec. |
|---|---|---|---|---|---|
| P1 | PostgreSQL table creation in bulk copy | G1, Q10 | `CreateTable` creates the table instead of throwing | `NpgsqlBulkOptions` constructor and `ColumnDefinitionOptions` property; PostgreSQL `ColumnDefinitionOptions` gets a purpose | Do |
| P2 | Insert-and-get-ID on both providers, from Common | G2, Q18 | none on SQL Server | `CreateInsertAndGetID` on `ICommandCreator`; virtual `CommandTextGenerator.InsertAndGetID` | Do |
| P3 | `DateTimeOffset` on PostgreSQL | G3, part of Q87 | reads and writes that fail today work | none | Do |
| P4 | PostgreSQL bulk copy with the column types | G4, Q85 | none by default (opt-in) | `NpgsqlBulkOptions.UseColumnTypes` | Your call (later) |
| P5 | Bulk copy timeout on both | G5 | none | `BulkCopyTimeout` moves to `BulkOptions` | Do |
| P6 | README, XML docs, register, phase notes | – | – | – | Do |
| P7 | `CreateTable` from another database's reader, on both | cross-provider gap | SQL Server: an exception becomes a created table | none | Your call (yes) |

### P1 PostgreSQL table creation in bulk copy

What gets built:

- An internal PostgreSQL column definition with this type mapping (the SQL Server mapping is shown for
  comparison):

  | Member type | PostgreSQL | SQL Server |
  |---|---|---|
  | `bool` | `boolean` | `bit` |
  | `byte`, `short` | `smallint` | `tinyint`, `smallint` |
  | `int`, `long` | `integer`, `bigint` | `int`, `bigint` |
  | `float`, `double` | `real`, `double precision` | `real`, `float` |
  | `decimal` | `numeric(p, s)` | `decimal(p, s)` |
  | `Guid` | `uuid` | `uniqueidentifier` |
  | `DateTime` | `timestamp(n)`, see decision 2 | `datetime2(n)` |
  | `DateTimeOffset` | `timestamptz(n)` | `datetimeoffset(n)` |
  | `DateOnly`, `TimeOnly` | `date`, `time(n)` | `date`, `time(n)` |
  | `TimeSpan` | `interval`, see decision 3 | `time(n)` |
  | `string` | `varchar(n)`, or `text` for -1 | `nvarchar(n)` / `nvarchar(max)` |
  | `byte[]` | `bytea` (no length) | `varbinary(n)` / `varbinary(max)` |

  The mapping is chosen so that what binary `COPY` writes for each CLR type fits the column it creates.
  This way P1 does not depend on P4.
- **Source of the column types** (changed after the probes). By default, as on SQL Server, they come
  from the reader's column schema, but only when that schema comes from Npgsql (its columns are
  `NpgsqlDbColumn`, which a wrapper around an Npgsql reader passes on). Npgsql reports names without
  their size, like `character varying` and `numeric`, so the size is added from the schema:
  `ColumnSize` for `character varying` and `character`, `NumericPrecision` / `NumericScale` for
  `numeric`, and `NumericPrecision` for timestamps and `time`. A type with no reported size is declared
  without one. Arrays (`integer[]`) and user-defined types (`public.mood`) are written as reported. Any
  other reader (SQL Server, `UniversalDataReader`, a `DataTableReader`) gets the column types from the
  fields' CLR types, and so does `IgnoreDataReaderSchemaTable`.
- `CheckTableIfNotExistsBeforeCreation` becomes `create table if not exists`. A temporary table can be
  named as `pg_temp.name`; this goes in the README.
- The primary key comes from `ColumnDefinitionOptions.PrimaryKey` and the reader's `IsKey`, as on SQL
  Server (Q103, Q128). From the CLR types, nullability follows the SQL Server rules: reference types and
  `Nullable<T>` are nullable, and Q104 applies here too. From an Npgsql schema, a column is `not null`
  only when the schema says so. Npgsql reports `AllowDBNull` and `IsKey` only for readers opened with
  `KeyInfo`, which FastSql does not do, so an unknown nullability means nullable.
- **Field list problems.** A missing field, an unsupported type or a duplicate gives the same messages as
  SQL Server (phase 4, I8). That code moves to Common so both providers share it. To keep the public API
  as it is, the two provider assemblies get `InternalsVisibleTo`. Like SQL Server, the field list is
  compared with case (Q130). Q130 stays your open decision, and whatever you decide will apply to both
  providers.
- **`NpgsqlBulkOptions`.** It gets a `ColumnDefinitionOptions` property. Its constructor gets the
  parameters `SqlBulkOptions` has for table creation (`createTable`, `columnDefinitionOptions`,
  `ignoreDataReaderSchemaTable`, `checkTableIfNotExistsBeforeCreation`), plus `bulkCopyTimeout` from P5.
  Copying, `SetDefaults` and `Clone` handle the new property. The existing constructor has to change: a
  second constructor with all parameters optional would make `new NpgsqlBulkOptions()` ambiguous. Code
  that compiles today still compiles (the new parameters come last).
- **PostgreSQL `ColumnDefinitionOptions`.** The "not used" remarks go. The presets keep their names, but
  their date and time scale becomes 6, because PostgreSQL rejects more than 6 digits; a user-set 7 is
  capped at 6. `BytesMaxLength` is documented as having no effect. The class is unused today, so changing
  it affects no one.

Tests: the four `NotImplementedException` tests are replaced by a PostgreSQL table creation suite that
mirrors the SQL Server one. It covers creating from a query, from a reader and from an enumerable; with
and without the schema table; creating twice with the existence check; a `pg_temp` table; a primary key;
an unsupported type with `ThrowIfUnsupportedType` on and off; the field list messages; and SQL Server
data copied into a new PostgreSQL table. Unit tests check the generated script for every type and preset.

### P2 Insert-and-get-ID on both providers

- In Common:
  - `CreateInsertAndGetID<T>` on `ICommandCreator`, with the two overloads the SQL Server method has.
  - `CommandCompilator.CompileInsertAndGetID<T>`, cached like `CompileInsert`.
  - A virtual `CommandTextGenerator.InsertAndGetID(tableName, fields)`. By default it throws
    `NotSupportedException`, so custom generators still compile.
- SQL Server: `SqlCommandTextGenerator.InsertAndGetID` becomes the override, with the same text. The two
  SQL Server extension methods stay, forwarding to Common, so existing code compiles to the same call.
  When both namespaces are imported and the receiver is an `ISqlCommandCreator`, C# picks the SQL Server
  method, so there is no ambiguity; a compile test will check this. A context returned by
  `CreateContext` no longer needs a cast (the cast part of Q18).
- PostgreSQL text, decision 4 (changed after the probes, see [Probe results](#probe-results)):

  ```sql
  with inserted as (insert into {table} (...) values (...) returning *)
  select (to_jsonb(inserted) ->> c.attname)::bigint from inserted,
    (select attname from pg_attribute where attrelid = '{table}'::regclass and attnum > 0
     and not attisdropped and pg_get_serial_sequence('{table}', attname) is not null
     order by attnum limit 1) c;
  ```

  This returns the inserted row's own value of its `serial` or identity column, which the catalog
  lookup finds, so no column name is needed. Triggers cannot affect it, and an explicit value (where the
  column accepts one) comes back as given, as with `scope_identity()`. A table without such a column
  gives no row, so `ExecuteScalar` returns null, as on SQL Server; the row is still inserted. With two
  such columns, the first one wins. The table name is the text the user passes (quoted names work), with
  `'` escaped. The ID comes back as `bigint` on PostgreSQL and `decimal` on SQL Server;
  `ExecuteScalar<int>` and `ExecuteScalar<long>` read both. `ExecuteNonQuery` returns -1 on PostgreSQL
  (the statement is a `select`) and 1 on SQL Server.
- Tests: the SQL Server `InsertAndGetID` tests become a shared suite run against both providers. The
  PostgreSQL cases add `serial`, identity `by default` and `always`, a table with no identity, a trigger
  that inserts into another table, and quoted and schema-qualified names.

### P3 `DateTimeOffset` on PostgreSQL

- **Reading.** `ValueConverter` converts a `DateTime` of kind `Utc` into a `DateTimeOffset` with offset 0.
  This loses nothing, and it is what Npgsql 10 returns for `timestamptz`. Other kinds are not converted,
  because their meaning is unclear, and neither is a `DateTimeOffset` into a `DateTime` member (the rest
  of Q87 stays).
- **Writing** (decision 6). Npgsql rejects a `DateTimeOffset` with a non-zero offset. `timestamptz`
  stores only the instant, so converting the value to UTC first stores exactly what PostgreSQL would have
  stored anyway. This happens in `NpgsqlContextProvider.AddParamWithValue` for parameters. In bulk copy
  it happens only for columns whose field type is `DateTimeOffset`, decided once per copy, so other
  columns pay nothing per row.
- Tests: the Q87 tests that pin the failures become tests of the conversions, on every read and write
  path. The read benchmarks run before and after.

### P4 PostgreSQL bulk copy with the column types (your call)

The phase 4 probe showed that writing each value with the destination column's type fixes `int` into
`bigint` / `numeric`, `DateTime` into `date`, and `string` into `jsonb`. It also breaks one copy that
works today: a `DateTime` of unspecified kind into `timestamptz`. And by default the copy does not read
the table's columns, so doing this always would add a round trip to every copy. The proposal is a
`NpgsqlBulkOptions.UseColumnTypes` option, off by default. When it is on, the columns are read and each
value is written with `Write<T>(value, npgsqlDbType)`. It would get its own PR and a type matrix in the
tests. I lean towards doing it after P1 to P3, or leaving it for later; it is not needed for the other
items.

### P5 Bulk copy timeout on both

`BulkCopyTimeout` moves from `SqlBulkOptions` to `BulkOptions`. Moving a member to a base class keeps
compiled code working, and `SqlBulkOptions` keeps its constructor parameter. PostgreSQL sets
`NpgsqlBinaryImporter.Timeout` from it. 0 means no limit on both drivers (probe 4). The README will say
how the two drivers fail when the timeout expires: Npgsql also closes the connection.

### P6 Documentation

- README: table creation and insert-and-get-ID on both providers, plus a new "Differences between the
  databases" section built from D1 to D5.
- XML docs: `BulkOptions.CreateTable` and the PostgreSQL `ColumnDefinitionOptions`.
- Quirks register: Q10 and Q18 become fixed in phase 5, Q87 partly fixed, Q85 depends on P4, and the
  cross-provider `CreateTable` gets its own entry.
- `docs/parity-notes.md`: what was done, behavior and API changes, and benchmarks, as for phase 4.

### P7 `CreateTable` from another database's reader (your call)

This is P1's rule applied to SQL Server (changed after the probes): the reader's schema is used only
when it comes from Microsoft.Data.SqlClient (its columns are `SqlDbColumn`), otherwise the fields' CLR
types decide. SQL Server readers, and wrappers that pass their schema on, behave exactly as before. A
list of type names would not have done that: SqlClient reports user-defined CLR types as
`master.sys.hierarchyid`. A PostgreSQL query copied into a new SQL Server table then works instead of
failing. The pinned test
`WriteToServer_CreateTableOnSqlServerFromPostgreSqlQuery_ThrowsDbException` changes with it. I lean
yes: it is the same rule in both directions.

## Probes before implementation

These run in a scratch project against the test containers, and their results go into this plan, as in
phase 4:

1. Npgsql 10 column schema for the common types: `DataTypeName`, `ColumnSize`, `NumericPrecision` /
   `NumericScale` (including timestamps and `time`), `IsKey`, `AllowDBNull`.
2. `COPY` of every CLR type in the P1 table into the column type P1 creates for it, including `DateTime`
   of each kind into `timestamp` and `timestamptz`, and `DateTimeOffset` converted to UTC.
3. The P2 statement on `serial`, identity `by default` / `always`, no identity, a trigger that inserts
   into another table, and quoted and schema-qualified names.
4. `NpgsqlBinaryImporter.Timeout`: what 0 means, and the exception when it expires.
5. A compile check that the Common and SQL Server `CreateInsertAndGetID` methods resolve without
   ambiguity.

## Probe results

Run on 2026-11-20 with Npgsql 10.0.3 and Microsoft.Data.SqlClient 7.1.1, against the test images
(`postgres:18.6-alpine`, SQL Server 2022 CU27), from a scratch program that is not committed.

1. **Npgsql column schema.** Type names come without their size (`character varying`, `numeric`,
   `timestamp without time zone`, `time without time zone`). `ColumnSize` holds the length of
   `character varying(50)` and `character(5)`. `NumericPrecision` / `NumericScale` hold `numeric(10,2)`,
   and the precision of `timestamp(3)` and `time(2)` is in `NumericPrecision`. A type declared without a
   size reports none. Arrays are `integer[]`, and an enum is `public.mood`. `AllowDBNull`, `IsKey`,
   `IsAutoIncrement` and `IsIdentity` are reported only with `CommandBehavior.KeyInfo`; otherwise they are
   null. The columns are `Npgsql.Schema.NpgsqlDbColumn`. Result: P1 adds the sizes, and treats an unknown
   nullability as nullable.
2. **`COPY` into the P1 column types.** Every member type of the P1 table is copied without error:
   `byte` into `smallint`, `TimeSpan` into `interval`, `DateOnly` / `TimeOnly`, `decimal` into
   `numeric(38, 18)`, and so on. `TimeSpan` into `time` fails with `22P03: incorrect binary data format`,
   which confirms decision 3. A `DateTime` of any kind goes into both `timestamp` and `timestamptz`,
   because binary `COPY` sends the same format for both and the server takes it as wall clock or UTC. So
   with decision 2, bulk copy refuses no kind of `DateTime`, and the note in decision 2 is wrong for it.
   A `DateTimeOffset` with offset +03:00 fails with `ArgumentException`; converted to UTC, it stores
   the same instant.
3. **The insert-and-get-ID statement.** The planned `currval(pg_get_serial_sequence(...))` returned the
   right ID for `serial`, identity `by default` and `always`, quoted and schema-qualified names, and with
   a trigger that inserts into another table (where `lastval()` returned the other table's ID). But an
   insert that gives the `serial` column a value itself fails in a new session
   (`55000: currval ... is not yet defined in this session`), and it would return a stale ID later in a
   session. So P2 now uses `insert ... returning *` with the same catalog lookup. It returned the exact ID
   in every probed case: an explicit `1000` came back as `1000`; a table without a sequence returned null
   and still inserted the row; a missing table gave `42P01`; `smallserial` with a 100 KB `bytea` worked;
   with two `serial` columns the first won. `ExecuteNonQuery` returns -1 for it.
4. **`NpgsqlBinaryImporter.Timeout`.** `TimeSpan.Zero` means no limit (as 0 does for `SqlBulkCopy`).
   When it is not set, the connection's command timeout applies (30 s by default, the same as
   `SqlBulkCopy`). When it expires, `NpgsqlException` "Exception while reading from stream" is thrown,
   with an inner `TimeoutException`, and Npgsql closes the connection.
5. **Overload resolution.** With both namespaces imported, a `SqlConnectionContext` or
   `ISqlCommandCreator` receiver calls the SQL Server methods; a `DbContext` (what `CreateContext`
   returns) or `ICommandCreator` receiver calls the Common ones. There is no ambiguity, with or without
   `QueryOptions` and ignore lists.

Also seen: a `DateTimeOffset` parameter with a non-zero offset makes Npgsql close the connection as well
as throw. P3 removes that case. SqlClient reports alias types by their base type (`nvarchar`), `rowversion`
as `timestamp`, and user-defined CLR types as `master.sys.hierarchyid`; this is why P7 changed from a
list of type names to the driver check.

## Public API

- Common, new: `CommandTextGenerator.InsertAndGetID` (virtual), `CommandCreatorExtensions.CreateInsertAndGetID<T>`
  (2 overloads), `CommandCompilator.CompileInsertAndGetID<T>` (plus the `proto` overload the other
  `Compile...` methods have).
- Common, moved: `BulkOptions.BulkCopyTimeout` (from `SqlBulkOptions`).
- SQL Server: `SqlCommandTextGenerator.InsertAndGetID` becomes `override`; `SqlBulkOptions.BulkCopyTimeout`
  is now inherited.
- PostgreSQL: `NpgsqlBulkOptions` constructor parameters and its `ColumnDefinitionOptions` property;
  `NpgsqlCommandTextGenerator.InsertAndGetID` override; new preset values in `ColumnDefinitionOptions`;
  `NpgsqlBulkOptions.UseColumnTypes` only with P4.

## Behavior changes for users

- PostgreSQL `CreateTable` creates the table (it threw `NotImplementedException`).
- PostgreSQL `timestamptz` reads into `DateTimeOffset` members (it threw `InvalidCastException`).
- PostgreSQL accepts `DateTimeOffset` values with any offset and stores the instant (Npgsql threw
  `ArgumentException`).
- With P7: SQL Server `CreateTable` from a non-SQL Server reader creates the table (it threw
  `SqlException`).

## Delivery

Branches off `modernization`, one worktree and one PR into `modernization` each:

1. `parity/plan`: this plan, with the probe results.
2. Bulk copy: P1, P5, and P7 if you want it.
3. Insert-and-get-ID: P2.
4. `DateTimeOffset`: P3.
5. P4 if you want it now.
6. Each PR updates the README and the register for its own items. The last one adds
   `docs/parity-notes.md`.

## Decisions needed

Answered on 2026-11-20: as recommended. That is: 1 yes (P1, P2, P3, P5, P6); 2 `timestamp`; 3 `interval`;
4 `currval(pg_get_serial_sequence(...))`; 5 keep the SQL Server methods as forwarders; 6 yes; 7 later
(not in this phase); 8 yes (P7 is in); 9 nothing; 10 the PR split as proposed.

1. **The recommended set** (P1, P2, P3, P5, P6): go ahead as proposed, or what changes?
2. **`DateTime` column type in P1:** `timestamp` (I lean this way: it matches `datetime2`, which has no
   time zone, and it fits the `Unspecified` and `Local` values that are most common, including everything
   read from SQL Server) or `timestamptz`. Either way Npgsql refuses one kind on write: `timestamp`
   refuses `Utc`, and `timestamptz` refuses the others. (The probes showed this is wrong for bulk copy,
   which takes every kind into both; see probe 2.)
3. **`TimeSpan` column type in P1:** `interval` (I lean this way: it is what `COPY` writes for a
   `TimeSpan`) or `time` (as on SQL Server, but then copies need P4).
4. **P2 on PostgreSQL:** `currval(pg_get_serial_sequence(...))` (I lean this way: scoped like
   `scope_identity()`) or `lastval()` (simpler, but triggers that use other sequences affect it, like
   `@@IDENTITY`).
5. **P2, the SQL Server methods:** keep them as forwarders (I lean this way), or mark them `[Obsolete]`
   for removal in 2.0.
6. **P3 writing:** convert `DateTimeOffset` to UTC for PostgreSQL, yes (I lean this way) or no.
7. **P4:** now, later, or not at all.
8. **P7:** yes or no.
9. Anything from [Differences that stay](#differences-that-stay) that you want in this phase: a
   PostgreSQL `CreateFunction` (D1), identity handling like SQL Server's in `COPY` (D3), `MERGE` as an
   opt-in (D4).
10. **The PR split** above: fine, or another cut?
