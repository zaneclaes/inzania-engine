# `inzania-engine/ci` — reusable git hooks

Checks that belong at *commit* or *push* time rather than at edit time, shared by every repo that
vendors the engine. They are the counterpart to `.claude/hooks/` (see `../Docs/data-design.md` →
Enforcement): those guard an agent's individual edits and cannot see a change made in an IDE, by a
merge, or by a contributor not driving Claude. These run once per commit or push, on whatever is
actually about to land.

| Hook | Git phase | What it blocks |
|---|---|---|
| `PendingMigrations.cs` | `pre-commit` | A commit whose EF model is ahead of its migrations — i.e. `dotnet ef migrations add <Name>` would *not* be a no-op. |
| `SubmodulesPushed.cs` | `pre-push` | A push whose commits point a submodule at an object that submodule's own remote does not have. |

## `PendingMigrations.cs`

`data-design.md` §6 makes `dotnet ef migrations add` the only producer of schema changes, and the app
applies migrations itself at start-up (`ZHostApp.PrepareAsync` → `DataProvider.MigrateDatabaseAsync`).
Nothing else fails loudly when a model change lands without its migration: the build is green, review
sees a normal-looking model edit, and the first symptom is a running pod querying a column the
database does not have. The check is `dotnet ef migrations has-pending-model-changes`, which diffs the
compiled model against the committed `ModelSnapshot` and needs no database connection.

Costs one build of each configured startup project (~30 s for Chordzy's two contexts), so it is gated
on the staged file list: a docs-, content- or asset-only commit skips it entirely.

### Wiring it into a repo

It is a .NET 10 file-based app with a `#!/usr/bin/env dotnet` shebang. It **cannot** be symlinked
straight to `.git/hooks/pre-commit`: the kernel hands the shebang the path git invoked (`pre-commit`,
no extension) and `dotnet` only accepts a file ending in `.cs`. A repo gets one `pre-commit` file
anyway, so call it from that repo's own hook — Chordzy does this in `ci/hooks/pre-commit`, installed
by `ci/install-hooks.sh`:

```sh
dotnet run "$ROOT/inzania-engine/ci/hooks/PendingMigrations.cs"
```

Run it by hand over the whole tree (ignoring the staged-file gate) with `-- --all`; that is also the
form for CI or for checking after a merge the hook never saw.

### Configuration

`<repo-root>/ci/migration-check.json` (override with `--config <path>` or
`IZ_MIGRATION_CHECK_CONFIG`). JSON with `//` comments and trailing commas allowed. Chordzy's copy is
`ci/migration-check.json`.

| Key | Default | Meaning |
|---|---|---|
| `contexts[]` | — | `{ project, startupProject?, context? }`. `startupProject` defaults to `project`; `context` may be omitted when the project has one. Repeated entries sharing a startup project build once and reuse it. |
| `configuration` | `Release` | Build configuration passed to `dotnet ef`. |
| `env` | `{}` | Environment for `dotnet ef` — normally `{"ASPNETCORE_ENVIRONMENT": "Development"}` so the design-time factory takes development paths. |
| `paths` | `["\\.(cs\|csproj\|props)$"]` | Staged paths that make the check worth running. Nothing matching ⇒ skip. |
| `reviewDoc` | *"the owning Migrations/README.md"* | Named in the failure message, so it points at the project's own review checklist. |

No config file at all is not an error: the hook says so and passes, so vendoring the engine does not
break a repo with no EF model yet.

### Behaviour

- **Pass** — every context matches its migrations, or the staged files cannot move the model.
- **Block (exit 1) — pending changes.** Prints the exact `dotnet ef migrations add …` command per
  context, then the review pointer.
- **Block (exit 1) — could not check.** A build failure, a missing design-time factory, no
  `dotnet-ef` tool. Passing here would silently retire the guard, so it blocks and prints the output.
- Bypass one commit with `SKIP_MIGRATION_CHECK=1 git commit …`.

### Deliberately-excluded contexts

A context whose migrations are already drifted (vendored third-party contexts are the usual case)
must be *left out of `contexts[]` with a comment saying why* rather than tolerated — listing it would
block every commit instead of catching a regression. Chordzy excludes Duende's `ConfigurationDbContext`
and `PersistedGrantDbContext` on exactly those grounds.

## `SubmodulesPushed.cs`

A superproject commit records a submodule's SHA. The two repos are pushed by separate commands, so the
natural mistake is to push the superproject first — or to push it after rewriting the submodule's
history out from under it. Nothing local notices: the object is still in your clone, `git status` is
clean, the build is green. It breaks for whoever clones next, which in practice is every CI agent:

```
fatal: remote error: upload-pack: not our ref <sha>
```

from `git fetch --recurse-submodules`, failing the checkout before a single build step runs. And it is
retroactive and permanent — the bad gitlink sits in pushed history, so *every later build* fails the
same way no matter how many good commits land on top, until the missing object is pushed.

So the check walks the commits actually being pushed, collects every gitlink value they record, and
verifies each one is reachable from that submodule's own remote (it fetches first, so a commit a
teammate has already pushed is not reported as missing).

### Wiring it into a repo

Same shape as `PendingMigrations.cs` — it cannot be symlinked to `.git/hooks/pre-push`, because the
kernel hands the shebang the path git invoked and `dotnet` only accepts a file ending in `.cs`. Call
it from the repo's own `pre-push`, forwarding the arguments *and* the stdin git wrote:

```sh
dotnet run "$ROOT/inzania-engine/ci/hooks/SubmodulesPushed.cs" -- "$@" <"$REFS"
```

⚠️ `pre-push` is normally **git-lfs's** hook. Taking the file over silently stops large-file uploads
unless you call `git lfs pre-push "$@"` yourself. Both halves read the ref list from stdin, which can
only be read once, so spool it to a temp file and redirect each from that — Chordzy's `ci/hooks/pre-push`
is the worked example, and its `ci/install-hooks.sh` refuses to install a hook that would displace an
lfs hook without chaining to it.

Run it by hand (or in CI) with `--all`, which ignores stdin and checks the gitlinks recorded in `HEAD`.

### Behaviour

- **Pass** — no submodules, nothing in the push moves a pointer, or every pointer is on its remote.
- **Block (exit 1)** — names each unpushed `<path> <sha>` with the commit's subject, and prints the
  `git -C <path> push` that fixes it. A gitlink whose object is not even in the local clone (rewritten
  away, as happens after an amend or rebase in the submodule) is reported separately, since pushing the
  submodule branch will not necessarily republish it.
- **Warn only** — uncommitted changes in a submodule. The recorded gitlink is what CI checks out, so
  local edits cannot break the build; they just mean the push carries less than it looks like it does.
- Bypass one push with `SKIP_SUBMODULE_CHECK=1 git push …`.
