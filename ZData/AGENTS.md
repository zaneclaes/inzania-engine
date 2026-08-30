# ZData — EF Core bridge (`IZ.Data`)

How a `DataObject` becomes a table, a query, and a migration. Pair with `../ZCore/AGENTS.md`
(object model, attributes, lazy resolution) and `../ZSchema/AGENTS.md` (GraphQL). Chordzy's
concrete pieces: `TuneData/TuneDbContext.cs` (the `DbSet`s), `TuneWeb/Server/TuneServerDbContext.cs`,
`TuneWeb/Server/Migrations/` (+ `AGENTS.md` there for the migration workflow).

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
   (`AddPooledDbContextFactory`) with Pomelo MySQL: `EnableRetryOnFailure(3)`,
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

## Design rules (checked by `.claude/hooks/DbGuard.cs`; see `TuneWeb/Server/Migrations/AGENTS.md`)

| Rule | Why here |
|---|---|
| Never write or edit files under `Migrations/` by hand; only `dotnet ef migrations add` | the ModelSnapshot must match the model exactly or the next migration diffs garbage |
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

## Known debt

Chordzy-specific index/query debt is tracked in `TuneWeb/Server/Migrations/AGENTS.md` (Known query/index debt).

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

**Database Monitoring (DBM) is NOT enabled** — `https://app.datadoghq.com/databases` lands on
the setup wizard, so there are no normalized query metrics, explain plans, or wait events from the
MySQL side. To enable it for the AWS RDS instance (no agent can run on RDS itself):
1. Run a Datadog Agent in the kops cluster (Helm chart or the existing DaemonSet) with the
   `mysql` integration: `instances: - host: <rds-endpoint> port: 3306 username: datadog
   password: <secret> dbm: true` plus `tags: [env:production, service:chordzy-mysql]` so DBM
   correlates with the APM service (`chordzy-mysql`).
2. In the RDS parameter group set `performance_schema=1`,
   `performance-schema-consumer-events-statements-current=ON`,
   `performance-schema-consumer-events-waits-current=ON`,
   `performance-schema-consumer-events-statements-history-long=ON`,
   `performance_schema_max_digest_length=4096`, `performance_schema_max_sql_text_length=4096`
   (reboot required), and add the `datadog` schema + `explain_statement` procedure from
   Datadog's MySQL DBM docs.
3. `CREATE USER datadog@'%'; GRANT REPLICATION CLIENT, PROCESS ON *.* TO datadog@'%'; GRANT
   SELECT ON performance_schema.* TO datadog@'%';` (+ `EXECUTE` on the datadog schema).
4. Optionally the `aws` integration with RDS enhanced monitoring for host metrics.
After that, the DBM query page is `https://app.datadoghq.com/databases/queries?env=production`.
