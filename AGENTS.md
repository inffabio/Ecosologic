# Agent Instructions

## Repository Layout

- The root solution is `Ecosologic.sln`; do not use the stale `backend/Ecosologic.sln` path found in older planning documents.
- Backend projects are under `backend/src/`: `Ecosologic.Domain` contains business rules, `Ecosologic.Application` contains use-case services, `Ecosologic.Infrastructure` contains PostgreSQL/Hangfire/integration code, and `Ecosologic.Api` is the ASP.NET entrypoint.
- Backend tests are under `backend/tests/`: domain tests and API/integration tests are separate projects.
- The Angular frontend is an independent application under `frontend/`; its entrypoint is `frontend/src/main.ts`.

## Commands

Run backend commands from the repository root:

```bash
dotnet restore Ecosologic.sln
dotnet build Ecosologic.sln
dotnet test Ecosologic.sln
```

Run a focused backend test with the project containing it:

```bash
dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter "FullyQualifiedName~TestClassOrMethod"
```

Run frontend commands from `frontend/`:

```bash
npm ci
npm run build
npm test
npm run e2e
```

Playwright starts the Angular dev server automatically on `127.0.0.1:4200`; `npm run e2e` therefore should normally be run from `frontend/` without starting another server.

## Local Development

- Start the local database before running the API: `docker compose up -d postgres`.
- The development database connection is PostgreSQL at `localhost:5432`; EF Core migrations run automatically when the API starts.
- The API requires `Auth__JwtKey` even in development. Run it with `dotnet run --project backend/src/Ecosologic.Api --launch-profile http`; the expected URL is `http://localhost:5157`.
- The frontend uses `http://localhost:5157/api` when opened on `localhost`; production-style hosts use the relative `/api` path through Nginx.
- The API has a fallback authenticated policy, so new public endpoints must explicitly opt out of it when appropriate.

## Operational Constraints

- Do not run or share `docker compose config` against the production `.env`; it prints interpolated secrets. Use dummy values as `monitoring/validate.sh` does.
- `ops/production.md` is the source for deployment, Hangfire, backup, restore, media-storage, and production secret-handling procedures. Do not duplicate those procedures here.
- Do not remove the production Data Protection volume or use `docker compose down -v` without an explicit maintenance plan; doing so invalidates protected session data.
- Validate monitoring configuration with `sh monitoring/validate.sh`; it requires Docker with the Compose v2 plugin.
- Keep secrets, production environment files, build output, coverage, Playwright results, and runtime uploads out of commits; the repository `.gitignore` already excludes them.

## Change Verification

- For backend changes, run the focused test project first, then `dotnet build Ecosologic.sln` and the relevant `dotnet test` command.
- For frontend changes, run the relevant unit tests and `npm run build`; run `npm run e2e` when browser behavior or routing changes.
- Preserve the existing Angular conventions in `frontend/.editorconfig` and the Prettier settings in `frontend/package.json`.
