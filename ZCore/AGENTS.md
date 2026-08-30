# ZCore — data/API object model and lazy resolution (`IZ.Core`)

Read with `ZData/AGENTS.md` (how objects become tables/queries) and `ZSchema/AGENTS.md` (how they
become GraphQL). ZCore itself has no EF or HotChocolate dependency; it is the vocabulary both use.

## Object hierarchy (`Data/`)

`ContextualObject` → `ApiObject` (anything on the wire) → `DataObject` (allowed in a `DbSet`) →
`ModelKey` → `ModelKey<TKey>` → **`ModelId`** (string PK, `[Key] [MaxLength(128)] Id`, ids from
`ModelId.GenerateId()` = 16 hex chars; child ids are `"{childId}-{parentId}"` via `CreateChildId`)
or **`ModelNumber`** (`long` PK). `TransientObject` is an `ApiObject` that is never stored
(`ZDbContext.CanStore` rejects it; the `Sanitize` pass throws on `Added` non-DataObjects).

Every object carries an `IZContext` (`Context`) — the unit of work: `Context.Data` is the
`IZDataRepository` (one `DbContext` per context, created lazily), `Context.Resolver` is the
batching loader, `Context.QueryFor<T>()` opens a query. Objects loaded from the DB get their
context set by `LoadDataModelsAsync` → `EnforceContext`.

## Attributes that shape the schema (`Data/Attributes/`)

| Attribute | On | Effect (see ZData for the EF side) |
|---|---|---|
| `[Table("Name")]` (System.ComponentModel) | class | required for every stored model; also marks it for `ZDbContext.OnModelCreating` |
| `[ApiIndex(nameof(A), nameof(B), IsUnique = …)]` | class, repeatable | becomes `HasIndex(A, B)` — the ONLY way to declare indexes |
| `[ApiKey(nameof(A), nameof(B))]` | class (root type only) | composite `HasKey` for models without `Id` |
| `[ApiParent(nameof(Child.Prop), throughModelType?, ApiDeleteBehavior)]` | navigation property | declares one-to-many / one-to-one / many-to-many (through entity) and the FK delete behaviour (**default `Cascade`** on the attribute) |
| `[NotMapped]` | property | never a column (use for resolved navigation caches) |
| `[OutputIgnore]` / `[InputIgnore]` / `[JsonIgnore]` | property | hidden from GraphQL output / input; also excluded from the descriptor scan |
| `[ApiFormat]`, `[ApiDocs]`, `[ApiAuthorize]`, `[ApiOrder]`, `[Cache]`, `[Observable]` | various | GraphQL fragment formats, docs, auth policy, wire order (P2P), client cache hints, metrics |

Conventions the generator relies on: a scalar `FooId` column + a navigation property `Foo` (the
descriptor maps `Foo` ↔ `fooId` by name); `ICreatedAt`/`IUpdatedAt` get timestamps and
**auto-indexes** on save/model-build (`ZData/Data/TimeStampData`).

## Lazy relationship loading — the N+1 hotspot (`Data/ApiObject.cs`)

`ResolveLocalId<T>(nameof(Foo))` / `ResolveForeignId` / `ResolveArray<T>(Id, nameof(Items),
nameof(Item.ParentId))` build a query `QueryFor<T>().FilterKeyIn(fk, keys)` and hand it to
`Context.Resolver`:

- Under GraphQL the resolver is `ZSchemaResolver` (ZSchema): keys from every object in the
  response are queued per `Type.field` and flushed as **one `WHERE fk IN (...)` query** after a
  1–22 ms delay. This is the codebase's DataLoader; it is what makes `song.scores { parts }` cheap.
- Everywhere else (`WorkDispatcher`, seeds, tests, CLI) the resolver is `ZDefaultResolver`
  (`Data/ZDefaultResolver.cs`): **one query per call**. Calling `Resolve*`/`Get*()` inside a
  `foreach` there is a textbook N+1. Batch explicitly: `QueryFor<T>().Filter(x => ids.Contains(x.Fk))
  .LoadLookupAsync(x => x.Fk)` or `.Fetch(x => x.Nav)` (EF `Include`, see ZData).
- `Resolution<T>` is a lazy `Task`; `.Required()` throws on null. Already-loaded values
  (`existing`) are cached into the loader so repeated resolution is free.

## Query surface (`Data/DataModelLoader.cs`, `Data/IZQueryable.cs`)

`IZQueryable<T>` wraps an EF `IQueryable` (Unity gets an in-memory `DataCacheRepository`).
Extension verbs — use these, not raw LINQ, so the call stays translatable and context-aware:
`Filter`, `SortAsc/SortDsc`, `Limit`, `Choose` (Select), `ChooseMany`, `FilterKeyIn`, `Fetch`
(Include); terminal: `LoadDataModelsAsync`, `LoadDataModelAsync`, `LoadRequiredDataModelAsync`,
`LoadCountAsync`, `LoadLongSumAsync`, `LoadDoubleSumAsync`, `LoadScalarAsync/LoadScalarsAsync`
(for `Choose` projections), `LoadLookupAsync`, `LoadDictionaryAsync`; writes: `Upsert`,
`UpsertId`, `UpsertModel`, `Context.Data.AddAsync/RemoveAsync/SaveAsync`.
`Context.LoadModelId<T>(id)` checks the change tracker (`GetMemoryModels`) before querying.

## Rules of thumb

- Never enumerate an `IZQueryable` synchronously (`foreach`, `.ToList()`) — it runs the query on
  the request thread and bypasses the repository semaphore; always `await Load…Async()`.
- Filter/sort/page in the query. Anything after `Load…Async()` runs in memory on the whole result.
- Project with `Choose` when you only need a few columns (e.g. ids for a follow-up batch).
- Keep predicates sargable: compare indexed columns to parameters; use `UsernameLower`-style
  normalized columns instead of `.ToLower()` in the lambda.
- A new `[Table]` needs `[ApiIndex]` for every column you filter or sort on (FK columns and
  `CreatedAt/UpdatedAt` are indexed automatically; nothing else is).
- `[ApiParent]` delete behaviour defaults to **Cascade**. Choose `Restrict`/`SetNull`
  deliberately for anything referenced from many places (users, scores) — a cascade from `Users`
  currently reaches ~118 FK edges.
