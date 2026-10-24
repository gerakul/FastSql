# Testing approach (modernization phase 0)

This document records the approved approach for covering FastSql with tests **before** the
modernization (upgrading .NET and package versions). The detailed list of tests is in
[testing-plan.md](testing-plan.md).

## Goal

Pin the current behavior of all public functionality with automated tests, without changing
the library code, so that after the upgrade we can prove that the behavior is preserved (or see
exactly where and why it changed).

## Decisions

| Topic | Decision |
|---|---|
| What is tested | The `netstandard2.0` build of the three libraries. `netstandard1.3` and `net46` will be dropped during modernization and are not tested. (Since phase 1 the libraries target `net10.0` only, see [upgrade-notes.md](upgrade-notes.md).) |
| Library code | Not changed. Tests use the public API only (plus protected members through subclassing, where the API is designed for it). No `InternalsVisibleTo`. |
| Kind of tests | Characterization tests: they pin what the code does today, including quirks and bugs. Quirks are tagged and listed, not fixed. |
| Unit tests | Run without Docker, using real ADO.NET objects that need no database (unopened connections, `DataTableReader`) and hand-written fakes. No mocking library. |
| Integration tests | Real SQL Server and PostgreSQL in Docker containers (Testcontainers). |
| Database versions | One version per provider: SQL Server 2022 and PostgreSQL 18, image tags pinned exactly. |
| CI | GitHub Actions on `ubuntu-latest` (Docker is preinstalled), running on pull requests into `modernization` and `master`. |

## Packages

| Purpose | Package | Version |
|---|---|---|
| Test framework | `xunit.v3` | 4.0.2 |
| SQL Server container | `Testcontainers.MsSql` | 4.16.0 |
| PostgreSQL container | `Testcontainers.PostgreSql` | 4.16.0 |
| Assertions | `AwesomeAssertions` (Apache 2.0 fork of FluentAssertions) | 9.6.0 |
| Public API snapshot | `PublicApiGenerator` | 11.5.4 |
| Code coverage | `Microsoft.Testing.Extensions.CodeCoverage` | 18.12.0 |

Container images:

- `mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04`
- `postgres:18.6-alpine`

Test runner: .NET 10 SDK `dotnet test` on Microsoft Testing Platform, enabled by `global.json`.

Considered and not chosen: NUnit, MSTest and TUnit (all workable; xUnit v3 has the simplest
container sharing through assembly fixtures and official Testcontainers support),
FluentAssertions 8+ (commercial license), Moq / NSubstitute (no mocking needed), Respawn (each
test class gets its own database instead), Verify (inline expected strings are easier to review
for SQL text).

## Feasibility check

Before this approach was approved, a throwaway prototype verified that:

- the libraries build with the .NET 10 SDK for all current targets;
- a `net10.0` test project consumes the `netstandard2.0` build;
- the legacy `IAsyncEnumerable<T>` from System.Interactive.Async 3.2 (which clashes by name with
  the one built into .NET) can be consumed through a single helper that uses an `extern alias`;
- System.Data.SqlClient 4.5.1 works against SQL Server 2022 and Npgsql 4.0.2 works against
  PostgreSQL 18.6 (queries, insert, insert with output, merge, bulk copy, stored procedures,
  async queries, transactions);
- an xUnit v3 class fixture can receive an assembly fixture in its constructor (needed for
  "one container per run, one database per test class");
- warm container start: SQL Server about 6.5 s, PostgreSQL about 2.5 s.

## Designing for the upgrade

The tests are written so that the expected phase 1 changes touch as few test files as possible:

1. **Async streams.** All reading of the library's `IAsyncEnumerable<T>` goes through one helper
   (`AsyncEnumerableCompat`). Moving to the built-in `IAsyncEnumerable<T>` should only change that
   file.
2. **SQL Server driver types.** `SqlParameter`, `SqlConnection` and `SqlBulkCopyOptions` (part of
   the public `SqlBulkOptions`) are used only in SQL Server specific test files. Elsewhere,
   parameters are created through `DbCommand.CreateParameter()`, so moving to
   Microsoft.Data.SqlClient is mostly a `using` change.
3. **Public API snapshot.** The public API of all three assemblies is stored in approved text
   files. Any API change made during the upgrade appears as a reviewed diff.
4. **Upgrade-sensitive tests are tagged.** Behavior known to depend on driver versions (for
   example `DateTime` handling in Npgsql 6+, stored procedure calls in Npgsql 7+) is tagged
   `UpgradeSensitive`, so expected differences are easy to find and discuss.
