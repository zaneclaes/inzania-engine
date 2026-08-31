# ZData — EF Core bridge (`IZ.Data`)

How a `DataObject` becomes a table, a query, and a migration. Pair with `../ZCore/README.md`
(object model, attributes, lazy resolution) and `../ZSchema/README.md` (GraphQL). Chordzy's
concrete pieces: `TuneData/TuneDbContext.cs` (the `DbSet`s), `TuneWeb/Server/TuneServerDbContext.cs`,
`TuneWeb/Server/Migrations/` (+ `README.md` there for the migration workflow).

## Pipeline

1. **Model → table.** `Storage/ZDbContext.OnModelCreating` walks every entity type that derives
   from `DataObject`, loads its `ZTypeDescriptor` (the same reflection IR GraphQL uses) and:
   - for each property with `[ApiParent]` configures `HasMany/WithOne`, `HasOne/WithOne`, or
     `HasMany/WithMany().UsingEntity(through)` with `OnDelete((DeleteBehavior) attr.DeleteBehavior)`;
   - applies every `[ApiIndex]` as `HasIndex(cols).IsUnique(...)` and `[ApiKey]` as `HasKey`;
   - calls an optional `public static ConfigureModel(IZContext, ModelBuilder)` on the model
     (none exist in Chordzy today — this is the escape hatch for fluent config);
   - then `TimeStampData.AutoIndex` adds an index on `CreatedAt` / `UpdatedAt` for every
     `ICreatedAt` / `IUpdatedAt` entity.
   Everything else (column types, FK columns `FooId`, `[MaxLength]`, `[NotMapped]`) is plain EF
   convention. No fluent config lives in `TuneDbContext` — attributes on the model are the source of truth.
2. **Provider.** `ZServer/Sql/ZMySql.AddZMySql<TDb>` registers a **pooled** `DbContext` factory
   (`AddPooledDbContextFactory`) with MySQL (Microting.EntityFrameworkCore.MySql 10.0.11, the maintained Pomelo fork — same `UseMySql` API): `EnableRetryOnFailure(3)`,
   **`QuerySplittingBehavior.SplitQuery`** (each `Include` becomes its own SELECT — no cartesian
   joins, but N round-trips per include level), `EnablePrimitiveCollectionsSupport`,
   `TranslateParameterizedCollectionsToConstants` (`Contains(list)` inlines values → plan cache
   churn for big lists), `EnableSensitiveDataLogging` + `EnableDetailedErrors` (**always on, incl.
   production** — parameter values reach logs). `MySqlOptions` reads `MySQL:Version` and
   `MySQL:Connection:*` from configuration; `Connection:UtcIntercept=true` would prepend
   `SET time_zone='+00:00';` to every command (`Providers/Interceptors/UtcTimeInterceptor`) — not
   enabled in Chordzy. SQLite bits (`Providers/Sqlite`) are commented-out leftovers.
3. **Repository / unit of work.** `Storage/ZEfCoreDataRepository<TDb>` is scoped per `IZContext`
   and creates the `DbContext` lazily. `QueryFor<T>(tracking)` returns a `DataModelQueryable<T>`
   over the `DbSet` (`DataModelTracking.Full` default; `None` = `AsNoTracking`;
   `IdentityResolution`). Every terminal operation (`ExecuteListAsync`, `…CountAsync`,
   `…SumAsync`, `SaveAsync`, `AddAsync`, `RemoveAsync`) runs inside a **per-repository
   `SemaphoreSlim(1,1)`** (`ZCore/Data/DataRepositoryBase.ExecuteLocked`) — a `DbContext` is not
   thread-safe, so parallel `Task.WhenAll` on one context serializes anyway; open a child context
   if you really need parallel DB work. `Sanitize` runs after loads and before saves: it attaches
   the context to loaded objects and rejects `Added` entities that are not `DataObject`s.
4. **Includes.** `Fetch(x => x.Nav)` (`ZCore/Api/IPreFetched.Fetch`) → `QueryInclude` →
   EF `Include`; `QueryThenInclude` / `QueryThenIncludeMany` → `ThenInclude`. Results are
   `IPreFetched<TEntity,TProp>` and still `IZQueryable`, so `Filter` etc. chain after them.
5. **Saving.** `ZDbContext.SaveChanges(Async)` first runs `UpdateChanges`: stamps
   `UpdatedAt`/`CreatedAt` (`TimeStampData.OnModelChanging`), sanitizes, and calls
   `IAutoUpdate.OnSavingData(DataState)` on changed entities. `Context.Data.SaveIfNeededAsync()`
   is the cheap no-op when nothing changed. `Rollback()` = `RejectChanges()`.
6. **Migrations at boot.** `Providers/DataProvider.MigrateDatabaseAsync<TDb>` is awaited in
   `ZServer/ZHostApp.PrepareAsync` **before** the app serves traffic (`Database.MigrateAsync()`),
   then `SeedDatabaseAsync(DataSeeds)` runs fire-and-forget. Consequences: every deploy applies
   pending migrations automatically (two replicas starting together can race — migrations must be
   idempotent/additive), and a bad migration blocks startup for the whole deployment.

## Design rules (canonical set: `../Docs/data-design.md`; enforced by `../.claude/hooks/`: `DbGuard.cs` + `MigrationGuard.cs` + `IndexAudit.cs`; see `TuneWeb/Server/Migrations/README.md`)

| Rule | Why here |
|---|---|
| Never hand-roll a schema change — no editing `Migrations/`, no `ALTER TABLE` in a SQL client, no DDL in `ExecuteSqlRaw`; only `dotnet ef migrations add` (`MigrationGuard.cs` blocks every route, shell writes included) | the ModelSnapshot must match the model exactly or the next migration diffs garbage; DDL applied by hand exists in one environment only (`../Docs/data-design.md` §6) |
| No DB call inside a loop (`Resolve*`, `Load*Async`, `QueryFor`) — batch with `FilterKeyIn`/`Contains`, `Fetch`, or the resolver | outside GraphQL the resolver is `ZDefaultResolver` = one query per call |
| Filter, sort and page in the query; never `Load…Async()` then `.Where/.OrderBy/.Take` | the full table streams through the app |
| Every new `[Table]` gets `[ApiIndex]` for its `Filter`/`SortAsc` columns; composite index order = equality columns first, then range/sort | only FK, PK, `CreatedAt`, `UpdatedAt` are indexed by default |
| Keep predicates sargable: no `.ToLower()`/`.Trim()`/function calls on the column side; no leading-wildcard `Contains` | MySQL cannot use a B-tree index through a function |
| Cap `Include`/`Fetch` depth at 3 and prefer keyed batches for wide collections | `SplitQuery` turns each level into another round-trip; wide joins multiply rows |
| Use `.LoadCountAsync()`/`.Any()` on a filtered query, not `.Count() > 0` on a list | COUNT of a materialized list already paid for the rows |
| Bound background loads (`Limit(n)`, incremental cursors on `CreatedAt`/`UpdatedAt`) | `WorkDispatcher` runs every 5 min; a whole-table `LoadDataModelsAsync` grows without bound |
| `DataModelTracking.None` for read-only, large result sets | change tracking costs memory per row |
| `[ApiParent]` delete behaviour is a design choice — cascades are the default | deleting a `TuneUser` today cascades through ~118 FK edges |
| Text/blob columns never go in an index (`ModelId.MaxIndexableStringLength` = 768 chars) | MySQL InnoDB index prefix limit (3072 bytes utf8mb4) |
| Never store a raw `bool` column; booleans are bits in one `[Flags]` enum column with a numeric `*Val` wire mirror | one integer column for N booleans, no migration per flag; full pattern + wire rules in `../Docs/data-design.md` §1 |
| Never index or hot-path-filter a flags column (`Flags & x` / `HasFlag`) | a B-tree cannot serve a bitwise predicate — the query scans and the index is pure write cost |
| N:M = explicit join entity (`[ApiKey(fkA, fkB)]`, two FK/nav pairs + `Get*()` resolvers), not EF skip navigations | skip-nav joins are invisible to GraphQL and the batching resolver; details in `../Docs/data-design.md` §4 |

## Known debt

Chordzy-specific index/query debt is tracked in `TuneWeb/Server/Migrations/README.md` (Known query/index debt).

## Inspecting queries

**Locally.** In `TuneWeb/Server/appsettings.Development.json` add under `Serilog:MinimumLevel:Override`:
`"Microsoft.EntityFrameworkCore.Database.Command": "Information"` — every SQL statement with
parameters (sensitive logging is already on) prints to the console. For one query:
`q.ToQueryString()` on the underlying `IQueryable` (cast `IZQueryable<T>` to `IQueryable<T>`).
Explain a statement against MySQL with `EXPLAIN ANALYZE <sql>` from any client; the schema is in
`TuneWeb/Server/Migrations/TuneServerDbContextModelSnapshot.cs` (indexes = `HasIndex` lines).

**Production (Datadog, org "LFG (Furballs)", site `app.datadoghq.com`).** The .NET tracer in the
server image (`TuneWeb/Server/Docker/Dockerfile`, `CORECLR_ENABLE_PROFILING`) auto-instruments
MySqlConnector, so every SQL statement is a `mysql.query` span on service **`chordzy-mysql`**
(parent service `chordzy`; auth server `chordzyauth`; env tag **`production`** / `staging`;
~55 k DB spans per week). Use:

- Service page (resources = normalized SQL, requests, p95, total time, errors):
  `https://app.datadoghq.com/apm/entity/service%3Achordzy-mysql?env=production&operationName=mysql.query&spanKind=client&start=<ms>&end=<ms>&paused=true`
- Trace/span explorer, DB spans only, group by statement (top list by total duration):
  `https://app.datadoghq.com/apm/traces?query=service%3Achordzy-mysql%20env%3Aproduction&agg_m=%40duration&agg_m_source=base&agg_t=sum&agg_q=resource_name&agg_q_source=base&viz=toplist&top_n=25&historicalData=true&start=<ms>&end=<ms>&paused=true`
  (swap `agg_m=count&agg_t=count` for call counts — that is how you spot N+1: many identical
  statements per trace, or `@_span.count` ≫ 10 on `service:chordzy` traces).
- Slow statements: add `@duration:>100ms` to the query. Per-request breakdown: open any
  `chordzy` trace and count `chordzy-mysql` children.
- Logs: `https://app.datadoghq.com/logs?query=service%3Achordzy%20env%3Aproduction` — no EF
  command logs exist (see level above); exceptions and `[DB]` warnings do.
- `<ms>` = epoch milliseconds; the UI accepts `start`/`end` and `paused=true` for a fixed window.

**Database Monitoring (DBM) was enabled 2026-08-30** for the RDS instance `inzania-sql`
(MySQL 8.4, hosts production + staging + auth schemas). Setup lives outside this repo, in the
moongate deployment tree (`$ZSYNC/moongate/datadog/inzania/values.yaml`): the `mysql` check runs
as a **cluster check** (`dbm: true`, tag `dbinstanceidentifier:inzania-sql`) dispatched by the
cluster agent to one node agent; the DB password is the Kubernetes Secret `btd/datadog-mysql`
resolved via the agent secret backend (`ENC[k8s_secret@…]`). On the DB side the `datadog` user
(REPLICATION CLIENT, PROCESS, SELECT on performance_schema, max 5 connections) plus the
`datadog` schema and `explain_statement` procedures (in `chordzy_production`, `chordzy_staging`,
`chordzy_auth`) exist. `options.replication: false` is set because agent 7.57's check still
issues `SHOW MASTER STATUS`, removed in MySQL 8.4.

- Fully live since 2026-08-31: the RDS parameter group `inzania-mysql84-dbm`
  (`performance_schema=1` + 4096 digest/sql-text lengths) is attached and the instance was
  rebooted; query metrics, query/activity samples (wait events, lock time, index usage) and
  explain plans all flow. DBM pages: `https://app.datadoghq.com/databases` (instance
  `inzania-sql`), query metrics at `https://app.datadoghq.com/databases/queries?env=production`.
- MCP route: `find_datadog_database_instances` (tags `dbinstanceidentifier:inzania-sql`) →
  `get_datadog_database_query_performance` / `..._statement` / `search_datadog_database_samples` /
  `get_datadog_database_recommendations`.
- Optionally the `aws` integration with RDS enhanced monitoring for host metrics (not set up).
