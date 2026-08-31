# IZ.* database & API design rules

Canonical data-modeling rules for any project built on `inzania-engine`. They diverge from
generic EF Core advice in specific, deliberate ways; each rule below states the mechanism that
makes it necessary. Enforcement is layered (see [Enforcement](#enforcement)):
`.claude/hooks/DbGuard.cs` (per-edit guard), `.claude/hooks/MigrationGuard.cs` (schema-change
guard) and `.claude/hooks/IndexAudit.cs` (whole-repo audit, also wired as a gated post-edit hook).

Companion deep-dives: `../ZCore/README.md` (object model, attributes, lazy resolution),
`../ZData/README.md` (EF pipeline, query inspection), `../ZSchema/README.md` (HotChocolate
binding, batching resolver). File references below use Chordzy (`Tune*`) as the reference
implementation.

## 1. Booleans: never a `bool` column — one `[Flags]` enum per table

A stored model never declares a persisted `bool`/`bool?` property. Boolean state lives as bits
in a single `[Flags]` enum column (conventionally named `Flags`), so N booleans cost one integer
column, adding a flag is not a migration, and the whole set travels as one number.

The canonical shape (`TuneCore/Users/Instruments/UserInstrument.cs`):

```csharp
[Flags]
public enum UserInstrumentFlags : uint {
  None        = 0,
  IsConnected = 1 << 0,
  IsDefault   = 1 << 1,
  IsSelected  = 1 << 2,
  HasBeenUsed = 1 << 3,
}

// on the entity:
[OutputIgnore] [InputIgnore]
public UserInstrumentFlags Flags { get; set; }

[NotMapped]
public uint FlagVal {                 // the ONLY wire surface for the bitfield
  get => (uint) Flags;
  set => Flags = (UserInstrumentFlags) value;
}

public bool AutoConnect => Flags.HasFlag(UserInstrumentFlags.IsSelected);  // read-only accessor
```

Rules, and why:

- **The enum declares `[Flags]`, explicit `1 << n` members, and an explicit base type**
  (`uint`/`ulong`). EF maps the enum to its underlying integer with no value converter, so the
  base type *is* the column type (`int unsigned` / `bigint unsigned`).
- **The enum property is `[OutputIgnore]` AND `[InputIgnore]`; the `[NotMapped]` `*Val`
  numeric mirror is the only thing on the wire.** This is not a style choice: `ZEnumType<T>`
  binds every enum as a GraphQL `enum` whose values are only the *declared* members
  (`ZSchema/Types/ZEnumType.cs`), so a combination like `IsConnected|IsSelected == 5` has no
  name — HotChocolate's `EnumType.Serialize` throws `SerializationException` on output, and its
  variable coercion rejects numeric literals on input. Bit combinations therefore **cannot**
  cross the GraphQL wire as the enum type; they go as a plain `UnsignedInt`/`UnsignedLong`
  scalar via `*Val`. (Historical evidence: the reverted `P5`/`E5` pseudo-name hack in
  `ZSchema/Conventions/ZNamingConventions.cs`.) `[OutputIgnore]` alone is NOT enough —
  `ZObjectDescriptor.LoadProperty` adds properties to GraphQL *inputs* unless `[InputIgnore]`
  is present, and a leaked flags enum on an input type fails coercion the moment a client sends
  a real bitmask.
- **The `*Val` cast type must match the enum's underlying type** (`(uint)` for `: uint`,
  `(ulong)` for `: ulong`). `ZObjectDescriptor.ConvertValue` and `EnumConverter<T>.Read` box
  numeric input as `int`; a mismatched cast path throws `InvalidCastException` at runtime.
- **Boolean accessors over flags are read-only** (expression-bodied `=> Flags.HasFlag(...)`),
  so EF never mistakes them for columns. Mutation happens explicitly at call sites:
  `x.Flags |= F.Foo;` / `x.Flags &= ~F.Foo;`. Add `[NotMapped]` only when the getter has a body
  block that could look mappable.
- **Plain (non-flags) enums are fine as columns** and serialize as `SCREAMING_SNAKE` GraphQL
  enum names. The `*Val` mirror is only required for `[Flags]` enums (and for any enum a client
  must round-trip numerically, e.g. `GradingOptions.Focus`).

### Flags and query performance (the one weakness — measured)

A B-tree index cannot serve a bitwise predicate. `WHERE (Flags & 4) <> 0` scans every row even
when the `Flags` column is indexed — MySQL only uses the index for whole-column equality/range.
Production evidence (Datadog, `service:chordzy-mysql`): `IpAddresses` has
`[ApiIndex(nameof(Flags))]`, yet its `WHERE (Flags & ?) <> ? AND (Flags & ?) <> ?` query
(~3.8k runs/week) full-scans; the index is pure write cost.

- Never `[ApiIndex]` a flags column.
- Never filter on flag bits (`HasFlag`, `&`) in a hot path (request resolver, recurring worker)
  against a growing table. On small bounded tables an occasional scan is fine — say so in a
  comment.
- If a bit becomes a hot filter, promote it: give it its own column (a plain enum, a nullable
  timestamp like `DeletedAt`/`DeactivatedAt`, or a MySQL generated column) and index that.
  Nullable-timestamp columns are the house idiom for "boolean + when" (`DeactivatedAt IS NULL`
  is sargable and carries more information than a bit).

## 2. Indexes: every recurring query is covered, and nothing else

Only the PK, FK columns, and `CreatedAt`/`UpdatedAt` (for `ICreatedAt`/`IUpdatedAt` models) are
indexed automatically. Everything else requires an explicit `[ApiIndex]` on the model class —
that attribute is the *only* way indexes are declared (no fluent config).

**The rule: any query that runs on a request path or a recurring worker must be served by an
index.** Slow queries are strictly worse than index write overhead in this architecture: MySQL
is shared by all tenants of the app server, every GraphQL resolver wave blocks on the slowest
`WHERE fk IN (...)`, and the repository semaphore serializes all queries in one context — one
table scan stalls the whole unit of work.

When an index is worth it:

| Add an index | Skip the index |
|---|---|
| Any `Filter`/`SortAsc` column combination used by a resolver, query method, or `WorkDispatcher` job | Tiny bounded reference tables (≲ a few hundred rows, e.g. seeded config) |
| Reverse lookups on the second column of a composite PK (the PK only covers its left prefix) | One-off admin/backfill queries — run them off-peak instead |
| Columns in recurring `COUNT`/`SUM` aggregates (the index makes them index-only scans) | Column combinations already covered by an existing index's left prefix |
| Uniqueness constraints (`IsUnique = true`) — these are correctness, not tuning | Write-hot tables where the candidate query is rare and bounded |
| | Text/blob columns (`MaxIndexableStringLength` = 768 chars) and flags columns (§1) |

Composite index column order: **equality columns first (most selective first), then the range or
sort column last.** `[ApiIndex(nameof(Role), nameof(LastActiveAt))]` serves
`WHERE Role = ? AND LastActiveAt >= ?` and `WHERE Role = ? ORDER BY LastActiveAt`; the reverse
order serves neither well. A comment naming the query the index serves
(`// Specific to user active query`) is house style — keep doing it, it's what lets the audit
and future readers retire dead indexes.

Keep predicates sargable or the index is wasted: no function calls, `.ToLower()`, string
concatenation, or arithmetic on the column side of a comparison (store a normalized column like
`UsernameLower` instead; compare concatenated keys by filtering each component column, not
`s.A + "-" + s.B`).

Verification loop: `.claude/hooks/IndexAudit.cs` cross-references every `Filter`/`SortAsc`/
`FilterKeyIn` site against declared indexes. In production, Datadog is ground truth — APM
service `<app>-mysql` spans (group by `resource_name`, sum `@duration`) and DBM query metrics /
explain plans on the instance (`https://app.datadoghq.com/databases`). A statement that is both
frequent and slow gets an index; an index no statement uses gets deleted.

## 3. Inheritance: abstract bases share columns; TPH only by convention

Two distinct patterns, both attribute-driven:

- **Abstract base without `[Table]` (the dominant pattern).** Invisible to EF; each concrete
  descendant declares its own `[Table]` and receives the base's columns in its own table. Use
  for shared FK+resolver bundles (`UserModelId`: `UserId` + `User` nav + batched `GetUser()`)
  and shared column sets (`ScorePartChild`, `UserSkill`). Attribute inheritance is deliberately
  asymmetric (`ZDbContext.OnModelCreating`): **`[ApiIndex]` on a base lands on every concrete
  table; `[ApiKey]` must sit on each root type only** (EF rejects keys configured on derived
  types).
- **TPH (single table + discriminator) when subclasses of a `[Table]` class are themselves
  `DbSet`s.** This falls out of EF convention — the codebase never calls `HasDiscriminator`.
  Requirements when you do this:
  - Declare the discriminator as a real column: `[MaxLength(32)] public string Discriminator`.
  - Declare the JSON side on the root: `[JsonPolymorphic(TypeDiscriminatorPropertyName =
    nameof(Discriminator))]` + one `[JsonDerivedType(typeof(Leaf), nameof(Leaf))]` per leaf.
    This is what pulls the leaf types into the descriptor tree (`ZObjectDescriptor.
    PolymorphicTypes`) and what lets `ZContextConverter` deserialize polymorphically. A TPH
    hierarchy without these (e.g. `NotificationInstance`, `JamSession`) has subtypes invisible
    to the API schema — acceptable only when the subtypes genuinely never cross the wire.
  - Keep `Discriminator` in output (not `[OutputIgnore]`) if any client dispatches on it.
  - TPT/TPC are never used. Don't introduce them.
- **GraphQL flattens all inheritance.** `ZApiTypeGenerator` excludes abstract/generic/non-public
  types; `ZSchema` emits no interfaces or unions — each leaf is an independent object type plus
  a `discriminator` string field. Never rely on GraphQL-side polymorphism; shared fields must be
  re-selected per leaf fragment.

## 4. N:M relationships: explicit join entities, resolved in batches

**The rule: model many-to-many as an explicit join entity** — not EF skip navigations. The
engine supports `[ApiParent(nameof(Far.Backs), typeof(Through))]` → `UsingEntity`, but a
skip-nav M:N is invisible to the GraphQL schema (object properties need `[ApiFormat]`+resolver
methods to become fields) and to the batching resolver, its FK columns are forced to the magic
`{PluralNavName}Id` naming, and any payload column on it (ordering, timestamps) is unreachable.
The one live instance (`UserSetList.Scores` ↔ `Score.SetLists` through `UserSetListScore`)
exists for EF-side `Fetch` only.

The canonical join entity (`TuneCore/Lessons/LessonProgressChallenge.cs`,
`TuneCore/WebPages/WebPageRelated.cs`):

```csharp
[Table("WidgetTags")]
[ApiKey(nameof(WidgetId), nameof(TagId))]          // composite PK: no surrogate Id, free (WidgetId, ...) index
public class WidgetTag : DataObject {
  [MaxLength(ModelId.MaxIdLength)] [ForeignKey(nameof(Widget))] [ApiFormat(CommonFormats.Full)]
  public string WidgetId { get; set; } = null!;
  public Widget Widget { get; set; } = null!;
  [ApiFormat(CommonFormats.Full)] public Task<Widget> GetWidget() =>
    ResolveLocalId<Widget>(nameof(Widget)).Required();     // batched under GraphQL

  [MaxLength(ModelId.MaxIdLength)] [ForeignKey(nameof(Tag))] [ApiFormat(CommonFormats.Full)]
  public string TagId { get; set; } = null!;
  public Tag Tag { get; set; } = null!;
  [ApiFormat(CommonFormats.Full)] public Task<Tag> GetTag() =>
    ResolveLocalId<Tag>(nameof(Tag)).Required();

  public int Order { get; set; }                    // join payload is first-class
}

// parent side (Widget):
[ApiParent(nameof(WidgetTag.Widget))]
public List<WidgetTag> Tags { get; set; } = new();
[ApiFormat] public Task<WidgetTag[]> GetTags() =>
  ResolveArray<WidgetTag>(Id, nameof(Tags), nameof(WidgetTag.WidgetId));
```

Rules, and why:

- **Composite `[ApiKey]` over the two FKs; no surrogate `ModelId` PK.** The composite key is
  the uniqueness constraint *and* a covering index for the left FK. A surrogate-key join with
  no `[ApiKey]`/`[ApiIndex]` (see `JamMemberLink`) has neither.
- **FK columns are auto-indexed by EF**, so the left-prefix side and the declared-FK side are
  covered; if you traverse the join from the *second* key with extra filters or ordering, add
  an explicit `[ApiIndex]` (e.g. `(TagId, Order)`).
- **Both `Get*()` resolvers, `nameof`-checked against the join entity itself.** `ResolveArray`'s
  third argument is a string FK name — `nameof(OtherClass.SessionId)` compiles and silently
  works until the property is renamed. Always `nameof(ThisJoinEntity.Fk)`.
- **Traversal cost over GraphQL is two batched waves** (`parent { tags { tag } }` = one
  `WHERE WidgetId IN (...)` + one `WHERE Id IN (...)` per wave) via `ZSchemaResolver` — this is
  the reason the explicit pattern scales. In bulk/offline code (where `ZDefaultResolver` gives
  one query per call) use `QueryFor<Join>().Fetch(j => j.Tag)` instead.
- A join needs a `DbSet` in the concrete `DbContext` like any other entity.

## 5. Over-the-wire performance checklist

- Bitfields travel as numbers (`*Val`), never as flag-enum values (§1).
- Every list field is bounded (`Limit`, cursor on `CreatedAt`/`UpdatedAt`); paging is manual.
- Relationship fields go through `Resolve*`/`Context.Resolver.Load*` so `ZSchemaResolver` can
  batch them into `WHERE fk IN (...)` waves; a custom `Get*()` that runs `QueryFor` directly
  executes once per parent.
- Fragments (`[ApiFormat]`) select the columns clients need — a leaner fragment is both less
  SQL and less JSON. Wide blob/text columns get their own `Full`-only format.
- Queries execute only as persisted operations; a new field is unreachable until it appears in
  a generated fragment.
- Verify in Datadog after shipping: span count per trace on the app service (a healthy request
  stays ≈ number of resolver waves, not number of rows), and per-statement totals on the
  `<app>-mysql` service.

## 6. Schema changes: `dotnet ef migrations add` is the only producer

The model is the schema's source of truth, and EF's `ModelSnapshot` is its record of what the
database already looks like. Every migration is generated as the diff between the two. That is why
a hand-rolled schema change is never a shortcut:

- **A hand-written (or hand-edited) migration** changes the database without changing the
  snapshot. The next `dotnet ef migrations add` diffs the model against a snapshot that no longer
  describes reality, so it re-creates columns that exist, drops ones it does not know about, or
  fails mid-deploy. The damage surfaces one or two migrations later, in the environment you did
  not test.
- **DDL typed into a SQL client** (`ALTER TABLE …` in `mysql -e`, a console session, a `.sql`
  script someone runs) exists in exactly one environment. Nothing replays it into the other
  environments or into a fresh developer database, and the snapshot never learns about it.
- **DDL from application code** (`ExecuteSqlRaw("CREATE TABLE …")`) runs on every replica, at an
  arbitrary point in the request lifecycle, with no ordering or once-only guarantee.

The workflow is therefore always: change the `[Table]` model → `dotnet ef migrations add
<PascalCaseName>` against the owning server project → review the generated `Up()` (it should
contain only what you intended; a pile of unrelated `AlterColumn`/`DropIndex` means the snapshot
was out of sync — stop and reconcile) → commit the model change and its migration **together**.
`dotnet ef migrations remove --force` undoes the last unapplied one (rewinding the snapshot with
it); `dotnet ef migrations script` shows the SQL without applying anything.

Migrations are applied by the app itself at start-up (`ZHostApp.PrepareAsync` →
`DataProvider.MigrateDatabaseAsync`) on every replica, before it serves traffic — there is no
manual apply step in CI, and a failing migration crash-loops the deployment instead of corrupting
a schema. Consequences for how you write them: prefer additive and idempotent steps (`AddColumn`
nullable or with a default, then backfill) over rename/drop in the same deploy, and make any
`migrationBuilder.Sql` data migration re-runnable, because two replicas can run it concurrently.

This rule is enforced by `.claude/hooks/MigrationGuard.cs` on all four routes (editor, shell,
SQL client, application code); see [Enforcement](#enforcement).

## Enforcement

Three reusable checks live in this repo under `.claude/hooks/` so every consuming project gets
them. They are hooks, not checklist items: what they cover does not belong in a project's
pre-commit routine as well. A fourth, `ci/hooks/PendingMigrations.cs`, runs at commit time
precisely because it covers what an edit-time hook cannot see — see
[the commit-time half](#the-commit-time-half-cihookspendingmigrationscs).

- **`DbGuard.cs`** — Claude Code `PreToolUse` hook (stdin JSON, exit 2 = block): blocks
  DB calls in loops and new persisted `bool` columns; warns on
  in-memory filtering, non-sargable predicates (including bitwise flag filters and string
  concatenation), unbounded loads, missing `[Flags]`/`[OutputIgnore]`/`[InputIgnore]` on
  bitfield enums, `[ApiIndex]` over flags columns, deep include chains, and new tables without
  indexes. Escape hatch: `// db-guard: allow` on the flagged line plus a justifying comment.
- **`MigrationGuard.cs`** — Claude Code `PreToolUse` hook over `Write|Edit|MultiEdit|Bash`
  (exit 2 = block), enforcing §6 on every route a schema change can take: a `*.cs` under a
  `Migrations/` folder or any `*ModelSnapshot.cs` (editor), a mutating shell command aimed at
  one (`>`, `tee`, `sed -i`, `cp`, `mv`, `rm`, `touch`, `patch`…), DDL passed to a SQL client
  (`mysql`/`mariadb`/`mysqlsh`/`mycli`/`psql` with `CREATE`/`ALTER`/`DROP`/`RENAME`/`TRUNCATE
  TABLE|INDEX|DATABASE` or `ADD`/`DROP COLUMN`), DDL inside `ExecuteSqlRaw`/`FromSqlRaw`, and
  hand-written `.sql` schema files. Warns on `dotnet ef database update|drop` (the app migrates
  itself at start-up) and on `migrationBuilder.Sql` data migrations (must be re-runnable).
  Reading migrations is untouched; `dotnet ef migrations add|remove|script|list` is the allowed
  path. Escape hatch: `migration-guard: allow` in the content or command, with a reason.
  Anything under `.claude/hooks/` is exempt so the hooks themselves stay editable.

  **The rule guards schema SHAPE, not DATA.** Structure is EF's to generate and a hand-written copy
  desyncs the `ModelSnapshot`; carrying rows across a change is the half EF cannot generate, and
  **data integrity outranks generated-shape purity** — a column dropped before its values are moved
  loses them permanently, and no regenerated migration brings them back. So a migration that also
  moves data (`UPDATE` / `INSERT INTO` / `DELETE FROM`, in a `migrationBuilder.Sql` block or
  otherwise) is exempt from M1 and may be hand-written and hand-tuned; a purely structural one is
  not, and a `*ModelSnapshot.cs` is never exempt however much SQL sits beside it. Write the data
  half so it is idempotent — guard each step on `information_schema` — so an interrupted run
  resumes instead of half-applying.
- **`IndexAudit.cs`** — whole-repo heuristic audit (`dotnet run IndexAudit.cs -- <repo-root>`):
  cross-references every `Filter`/`SortAsc`/`FilterKeyIn`/`ResolveArray` column against
  declared `[ApiIndex]`/`[ApiKey]`/auto-indexes, and re-checks the §1 flags rules with
  cross-file type knowledge. Advisory by default; `--strict` exits non-zero on findings.
  Accepted debt lives in `<repo-root>/.claude/IndexAudit.baseline` (one finding per line,
  `#` comments) so the audit stays green until *new* findings appear. `--hook` turns it into a
  Claude Code **PostToolUse** hook: it reads the tool JSON from stdin, exits instantly unless a
  `.cs` edit *touches* query surface or index attributes (`QueryFor`/`Filter`/`Sort*`/
  `FilterKeyIn`/`[ApiIndex]`/`[ApiKey]`/`[Table]` present in the new text or the text it replaced —
  added, removed or rewritten), and only then runs the full audit (~3 s) rooted at
  `$CLAUDE_PROJECT_DIR` — new findings come back as exit 2 on stderr.

Wiring into a consuming project (`.claude/settings.json`):

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Write|Edit|MultiEdit",
        "hooks": [
          { "type": "command", "command": "dotnet run \"$CLAUDE_PROJECT_DIR/inzania-engine/.claude/hooks/DbGuard.cs\"", "timeout": 60 }
        ]
      },
      {
        "matcher": "Write|Edit|MultiEdit|Bash",
        "hooks": [
          { "type": "command", "command": "dotnet run \"$CLAUDE_PROJECT_DIR/inzania-engine/.claude/hooks/MigrationGuard.cs\"", "timeout": 60 }
        ]
      }
    ],
    "PostToolUse": [
      {
        "matcher": "Write|Edit|MultiEdit",
        "hooks": [
          { "type": "command", "command": "dotnet run \"$CLAUDE_PROJECT_DIR/inzania-engine/.claude/hooks/IndexAudit.cs\" -- --hook", "timeout": 90 }
        ]
      }
    ]
  }
}
```

The `PostToolUse` gate is symmetric (it fires when an edit adds, removes *or* rewrites query
surface), so every agent edit that could change the audit result is already audited: a project's
pre-commit checklist should **not** repeat it. Run `dotnet run
inzania-engine/.claude/hooks/IndexAudit.cs -- .` by hand only for changes the hooks never saw —
edits made with shell commands instead of the edit tools, a merge, or another person's work.

### The commit-time half: `ci/hooks/PendingMigrations.cs`

Those three cover *how* a change is made, which is all a `PreToolUse` hook can see. One thing they
structurally cannot cover is the state the commit as a whole is in — specifically §6's closing rule,
that a model change and its migration land **together**. A model edited in an IDE, arriving through a
merge, or written by anyone not driving Claude reaches `git commit` with nobody having checked it,
and nothing downstream fails loudly: the build is green, review sees a normal model edit, and the
first symptom is a pod querying a column the database does not have (migrations are applied by the
app at start-up, so there is no CI step that would have noticed).

`ci/hooks/PendingMigrations.cs` is that check, at the one point every change passes through. It asks
exactly "would `dotnet ef migrations add <Name>` be a no-op" — `dotnet ef migrations
has-pending-model-changes`, which diffs the compiled model against the committed `ModelSnapshot` and
needs no database — and blocks the commit with the precise `migrations add` command when the answer
is no. It is a git hook rather than a Claude hook, is configured per repo by
`<repo-root>/ci/migration-check.json`, and is gated on the staged file list so content-only commits
skip it. Full contract: `../ci/README.md`.

## Known exceptions & debt (Chordzy)

- `PaymentCustomer.Delinquent` (`bool?`) — the sole persisted bool; mirrors a Stripe field.
  Grandfathered; migrating it is not worth a schema change. New code gets no such exemption.
- `InstrumentRegistrationInput.flags` / `WebCamRegistrationInput.flags` — flags enums leaked
  into input types (`[OutputIgnore]` without `[InputIgnore]`); clients send raw numbers that
  strict enum coercion rejects. Fixing requires a schema regen and breaks deployed clients that
  send the field — schedule with the next client-breaking release; new models must carry both
  attributes from day one.
- `IpAddresses`: `[ApiIndex(nameof(Flags))]` is unusable by its bitwise queries (§1) — remove
  the index and/or promote the hot bits when next touching the model.
- `JamMemberLink`: surrogate-key join with no `[ApiKey]`/uniqueness on
  `(SessionId, FromMemberId, ToMemberId)`.
- `ScorePart` composite-key batch uses `s.ScorePartId + "-" + s.ScorePartScoreId` concatenation
  in a predicate (non-sargable); filter per-component instead.
