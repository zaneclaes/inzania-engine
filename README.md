# inzania-engine — `IZ.*` engine layer (git submodule)

Reusable app/data/API/client/P2P foundation that Chordzy's `Tune*` projects subclass.
Separate repo (`git@github.com:zaneclaes/inzania-engine.git`): commit here **and** bump the
pointer in the parent. `git submodule update --init --recursive` after clone.
Build via the parent `Chordzy.sln` — `inzania-engine.sln` is stale (wrong paths).
Directory prefix `Z*` ↔ assembly/namespace `IZ.*`. net10 / C# 13 / nullable via
`Directory.Build.props`; `*.meta` excluded from compilation (Unity symlinks `ZCore`, `ZClient`,
`ZExt`, `ZP2P`, `ZSerilog` into `ChordzyGame/Assets`).

| Project | Purpose | Key files |
|---|---|---|
| `ZCore` (`IZ.Core`, no Z deps) | `ZApp` (product/env/urls/storage; sets global `ZEnv.App`), `IZContext : IServiceScope` (+ Root/Background/Child; `BaseContext` chains to parent), object hierarchy `ContextualObject → LogicBase` / `→ ApiObject → DataObject/TransientObject → ModelKey → ModelId/ModelNumber`, `Data/Attributes/` (`ApiDocs`, `ApiAuthorize`, `ApiKey`, `ApiIndex`, `ApiOrder`, `ApiParent`, `ApiFormat`, `ApiPacket`, `ApiTopic`, `EventMessage`, `Input/OutputIgnore`, `Cache`…), `Api/Types/*Descriptor` reflection IR + `Api/ZApiTypeGenerator.cs` (scans loaded assemblies; `GenerateSourceFiles` emits `TuneCore/Types`), `Api/ZQuery/ZMutation/ZSubscription` bases, `Data/Seeds/DataSeed`, `Data/ClientCache`, logging/metrics/analytics abstractions (`IZLogger`, `ZEnv.Log`), `Json/IZJson` (+System.Text.Json impl), `Utils/ZTask.cs` (struct wrapping UniTask under `Z_UNITY`, else `Task`), `ZEnv.Now` (UTC clock) | `Contexts/ZApp.cs`, `Contexts/IZContext.cs`, `Api/ZApiTypeGenerator.cs`, `Utils/ZTask.cs` |
| `ZSchema` (`IZ.Schema`) | HotChocolate binding of the descriptor IR: `ZSchema.cs` (`AddSchemaServices/Query`, explicit binding), `Types/ZObjectType`, `ZInputType`, `ZEnumType`, `ZModelIdType`, `Queries/ZQueryType`, `ZMutationType`, `ZSubscriptionType`, `ZSubscriber` (topic `Name_{param}` from `[ApiTopic]`, reflects into HotChocolate private API — HC upgrades can break it) | `ZSchema.cs`, `Queries/ZSubscriber.cs` |
| `ZData` (`IZ.Data`) | EF Core: `Storage/ZDbContext`, `ZEfCoreDataRepository<TDb>` (query/include/add/remove/save), `ZEfCoreDataFactory`, `Resolvers/` (`IZQueryable` over EF), `Providers/DataProvider` (`MigrateDatabaseAsync`, `SeedDatabaseAsync`), `Providers/Sqlite/`, `UtcTimeInterceptor`. SQLite (client) + Pomelo MySQL (server; options in ZServer) | `Storage/ZEfCoreDataRepository.cs` |
| `ZServer` (`IZ.Server`) | `ZHostApp<TDb> : ZApp` over `WebApplicationBuilder` (env, Serilog, `DataSeeds`, `/health/readiness`, `AddWorker<T>` forever-loops), `HostingExtensions` (`AddZServerCore/Http/GraphQl/Subscriptions`), `Graphql/ZHttpInterceptor` + `ZSocketInterceptor` (auth → `ClaimsPrincipal`), Redis-backed subscriptions, `Requests/ApiExceptionMiddleware`, `ZController`, SendGrid `Emails/`, `Sql/ZMySql`, ReCaptcha | `ZHostApp.cs`, `HostingExtensions.cs` |
| `ZClient` (`IZ.Client`) | `ZClientApp : ZApp` (singleton, `ClientId`, `SemVersion`), `ClientContext : RootContext` (identity store, visitor fallback, analytics), `ZGraphServerConnection : IServerConnection` (+ `ZStubJsonConnection` offline), hand-rolled `Queries/` (`GraphBuilder`, `GraphQuery`…) from the descriptor IR, `Networking/WebSockets/` (`IWebSocket` swap point for WebGL, graphql-transport-ws), `GoogleAnalytics/` | `ZClientApp.cs`, `ZGraphServerConnection.cs` |
| `ZP2P` (`IZ.P2P`) | Abstractions only — transport is LiteNetLib vendored in Unity. `Packets/ZPacket : ApiObject`, `ZPacketFormatter` (MessagePack: leading `[ApiPacket(byte)]` discriminator then fields in `[ApiOrder]`; scalars/strings/byte[]/enums only), `PacketSendStrategy` (numeric mirror of LiteNetLib `DeliveryMethod` — never renumber), `Shared/IZP2P<TMsg,TPacket,TSession,TMember>`, `Host/IZHost`, `Guest/IZGuest`, `IZP2PConnectionDelegate` (state machine Closed→Opening→LoadingSession→WaitingForPeer→Connected), `ZStunClient` (Google STUN, NAT detection), `ZNetworkInterface`, `IZP2PSessionApi` (signaling over the GraphQL subscription socket, 8-char session keys), 0.25 s pings for clock sync | `Packets/ZPacketFormatter.cs`, `Shared/IZP2P.cs` |
| `ZSerilog`, `ZDataDog` | Serilog `IZLogger`; DogStatsD metrics, DataDog tracing/log sink | — |
| `ZTests` | `ZTest<TApp> : LogicBase` (xunit output → Serilog, installs global root context), `ZTestApp`, `ZTestRootContext`, `ZTestActionContext`. Finalizer sleeps 15 s | `ZTest.cs` |
| `ZExt` | **Empty** placeholder referenced by ZSchema/ZServer (kept alive by `<Folder Include>`) | — |
| `LoggingMicrosoft`, `ZJsonNewtonsoft` | Dead — dangling project refs, won't restore | — |

Data layer deep-dives: `ZCore/README.md` (object model, attributes, lazy resolution), `ZData/README.md` (EF Core pipeline, design rules, inspecting queries in Datadog), `ZSchema/README.md` (HotChocolate binding, batching resolver). Migrations workflow: `TuneWeb/Server/Migrations/README.md`.

**`Docs/data-design.md`** is the canonical database/API design ruleset (flags enums instead of
bool columns, the numeric `*Val` wire mirror, index tradeoffs, inheritance, N:M through-joins) —
read it before adding or changing any stored model. It is enforced by three reusable tools in
`.claude/hooks/`, which consuming projects wire into their own `.claude/settings.json` (JSON in
the doc's Enforcement section): `DbGuard.cs` (PreToolUse — query/model anti-patterns),
`MigrationGuard.cs` (PreToolUse over edits *and* Bash — schema changes may only come from
`dotnet ef migrations add`; §6) and `IndexAudit.cs` (whole-repo audit with a per-project
`.claude/IndexAudit.baseline` for accepted debt; `--hook` makes it a gated PostToolUse hook that
fires when a `.cs` edit touches query surface in either direction). Because the hooks block these
mistakes at the edit, they are deliberately *not* repeated as pre-commit checklist items.

A fourth hook covers the API surface rather than the data layer: **`ApiRegistrationGuard.cs`**
(PostToolUse, `--hook`) flags any concrete `ZQueryBase`/`ZMutationBase`/`ZSubscriptionBase` class
with `IZResult` members that no registration class exposes as a public property — the one line
that turns a compiling class into a reachable endpoint (Chordzy: `TuneQuery`/`TuneMutation`/
`TuneSubscription` off `TuneRequest`). The registration class name is discovered structurally, so
nothing is project-specific.

⚠️ **Why the aggregate is required, and why GraphQL does not prove it.** An API class is invoked two
ways. Over the wire, HotChocolate resolves it reflectively (`ZApiTypeGenerator.CacheApiMethods` →
`ZSchema.AddZRequestDescriptors`), so an unregistered class can answer a GraphQL query perfectly
well. In C# it is invoked through **`Context.BeginRequest<TReq>()`** — the request tree used by
clients, caches, controllers and tests (`Context.BeginRequest<AuthQuery>().CurrentUser().Execute(…)`)
— and that tree *is* the aggregate. A class the aggregate never names is unreachable from C#, and on
a stripped client build the aggregate's public property is also the only static reference keeping the
type alive against the reflective path. So a live GraphQL endpoint is not evidence the class is
wired: test the registration, not the schema. The same check is available to test projects as
`ZTests/ZApiRegistration.FindUnregistered()` (reflection over loaded assemblies) so CI catches
edits the hooks never saw. Wire it next to `IndexAudit.cs`:

```json
{ "type": "command", "command": "dotnet run \"$CLAUDE_PROJECT_DIR/inzania-engine/.claude/hooks/ApiRegistrationGuard.cs\" -- --hook", "timeout": 90 }
```

A fifth hook guards endpoint **authorization**: **`ApiAuthGuard.cs`** (PreToolUse, next to DbGuard).
Auth is opt-in per endpoint — a public `IZResult<>` method with no `[ApiAuthorize]` is served to
anonymous callers (`ZSchema.AddApiAuthorization` only emits a directive when the descriptor carries
the attribute) — so the hook blocks an edit that leaves an unauthorized endpoint either
(a) dereferencing `Context.CurrentIdentity` (the identity is only guaranteed by a declared policy;
anonymous callers make the usual `CurrentIdentity!...` a null-deref or, worse, run the body as a
visitor/system identity) or (b) performing data writes (`Data.AddAsync/RemoveAsync/SaveAsync`,
`Upsert*` — an unauthenticated internet-facing write). The sanctioned patterns its messages point
to: user-called endpoints declare the weakest fitting `[ApiAuthorize(ZPolicy...)]` (virtual users
included); automation-called endpoints consume no user identity and instead restrict to the
caller's *system identity* role (an `IZIdentity` with a dedicated role — e.g. Chordzy's
`TuneSystemIdentity`; nothing project-specific is hard-coded here). Deliberately public endpoints
(login/signUp, public content reads) append `// api-auth: allow` plus a justification. The edit is
judged against the prospective file (on-disk + edit applied), so attributes above the edited
fragment are seen. CI-side twin: `ZTests/ZApiAuthorization.FindAnonymousEndpoints()` — assert it
against an explicit allowlist so every conscious omission is pinned (Chordzy:
`TuneTests/Lib/ApiAuthorizationTests`). Wire it beside DbGuard:

```json
{ "type": "command", "command": "dotnet run \"$CLAUDE_PROJECT_DIR/inzania-engine/.claude/hooks/ApiAuthGuard.cs\"", "timeout": 60 }
```

## Conventions and gotchas

- **API-shape rules** (violations of these shipped real security holes and wasted round-trips):
  1. *No new endpoint when an existing call already carries the context.* Clients call
     `currentSession`-style restore/auth endpoints on every boot with their `Installation`; data
     that must arrive "once per install/session" (attribution, registration, capability flags)
     rides those as an optional argument — a dedicated mutation for it is an extra round-trip, an
     extra auth surface, and a second code path to secure.
  2. *Decide the caller before writing the endpoint.* User-called (including pre-signup virtual
     users) ⇒ `[ApiAuthorize(ZPolicy...)]`, then consume `Context.CurrentIdentity`. Automation ⇒
     no user identity at all; the caller authenticates with the project's system identity (an
     `IZIdentity` with a dedicated role) and the endpoint restricts to that role. Never invent a
     third flow inside the body (e.g. upserting rows for whatever identity is present) — enforced
     by `ApiAuthGuard` below.
  3. *Client behaviour lives in the shared client layer* (`ZClient` subclass contexts), not in one
     platform's UI code — a feature implemented in a Blazor page or a Unity scene silently skips
     every other client type; the platform layer only *captures* platform-specific inputs into
     shared state (prefs), and the shared layer transports them.
- Behaviour via base classes (`ZApp`, `ZHostApp<TDb>`, `ZClientApp`, `ZDbContext`, `RootContext`,
  `ZQueryBase/ZMutationBase/ZSubscriptionBase`, `ZPacket`, `ZTest<T>`), metadata via attributes,
  intent via marker interfaces (`IHaveContext`, `IAmInternal`, `IForeverTask`, `IGetLogged`…).
  Logic classes take `IZContext` and extend `LogicBase`.
- No source generators or partial classes: codegen is out-of-band and destructive
  (`ZApiTypeGenerator.GenerateSourceFiles`, `ci/schema.sh`). Never hand-edit generated output.
- HotChocolate / StrawberryShake pinned at 15.1.12 across ZExt/ZSchema/ZServer/ZClient — keep in lockstep.
- `ZApiTypeGenerator.IsExternal()` filters assemblies by hardcoded name prefixes; add new
  third-party deps there if their types leak into the schema scan. Only public, non-abstract,
  non-generic types are included. A `[TYPES] generating type-map` warning at runtime means the
  pre-generated map wasn't supplied.
- Packet discriminators are a hand-managed global byte space; duplicates surface only as a
  runtime `Log.Error`. Changing `[ApiOrder]` breaks wire compatibility with deployed clients.
- Global statics (`ZEnv.App/Log/SpanBuilder`, `ZApi.TypeMap`) are set by `ZApp`'s constructor;
  two apps per process clobber each other. `ZApp.Settings/Auth/Storage` throw until `BuildAsync()`.
- Unity-safe code only in ZCore/ZClient/ZP2P/ZSerilog: `Z_UNITY` swaps `ZTask` onto UniTask and
  disables `global using` aliases (`Typedefs.cs`, guard `__IZ_TYPES__`); no blocking `.Result`,
  no threads/sync sockets on WebGL. `InternalsVisibleTo("inzania.Tests")` is vestigial.

## Before you finish a change

Follow the root `AGENTS.md` **Safe change protocol**. `ZCore`, `ZClient`, `ZExt`, `ZP2P`, `ZSerilog` are symlinked into Unity: after editing them run `dotnet build Chordzy.sln -c Release`, `dotnet test TuneTests/TuneTests.csproj -c Release`, and the Unity batchmode check; C# 10 / netstandard2.0 in those five projects, `ZTask` not `Task`, `Z_UNITY` gates. `ZData`/`ZSchema`/`ZServer`/`ZDataDog`/`ZTests` are .NET-only (build + tests suffice). Remember this is a submodule: commit here first, then bump the pointer in the parent repo.
