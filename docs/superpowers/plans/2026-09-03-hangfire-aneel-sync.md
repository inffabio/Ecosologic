# Hangfire e Sincronizacao ANEEL Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrar o processamento CRM para Hangfire e importar mensalmente os valores oficiais de TUSD/Fio B da ANEEL para Light e Enel Distribuicao Rio.

**Architecture:** Hangfire Server persistido no PostgreSQL executara jobs recorrentes com IDs estaveis. O CRM continuara usando `CrmNotificationService`; a nova sincronizacao sera dividida entre um adaptador ANEEL, normalizacao validada e reconciliacao transacional do catalogo tarifario. O catalogo permanecera versionado e a execucao sera auditada em uma tabela propria.

**Tech Stack:** .NET 9, ASP.NET Core, Hangfire Core, Hangfire PostgreSQL, Entity Framework Core, PostgreSQL, HttpClient, xUnit.

## Global Constraints

- Substituir completamente `CrmNotificationWorker` e `CrmNotificationLoop`.
- Usar PostgreSQL persistente para o Hangfire e para a auditoria da sincronizacao.
- Restringir Dashboard e endpoints de sincronizacao ao papel `Admin`.
- Filtrar somente Light, Enel Distribuicao Rio, B1/B2/B3 e TUSD Fio B.
- Preservar unidade original `R$/MWh`, origem, vigencia e hash.
- Reprocessamento identico deve ser idempotente.
- Falha externa nao pode remover a ultima tarifa valida.
- Nenhum valor de producao pode vir de fixture ou fallback inventado.
- Toda funcionalidade nova deve seguir TDD: teste falhando, implementacao minima, teste verde e regressao.

---

### Task 1: Adicionar Hangfire e migrar o job CRM

**Files:**
- Modify: `backend/src/Ecosologic.Api/Ecosologic.Api.csproj`
- Modify: `backend/src/Ecosologic.Infrastructure/Ecosologic.Infrastructure.csproj`
- Modify: `backend/src/Ecosologic.Api/Program.cs:1-106`
- Create: `backend/src/Ecosologic.Infrastructure/Jobs/HangfireJobRegistration.cs`
- Create: `backend/src/Ecosologic.Infrastructure/Jobs/CrmNotificationJob.cs`
- Delete: `backend/src/Ecosologic.Infrastructure/Crm/CrmNotificationWorker.cs`
- Delete: `backend/src/Ecosologic.Infrastructure/Crm/CrmNotificationLoop.cs`
- Modify: `backend/src/Ecosologic.Api/appsettings.json`
- Modify: `backend/src/Ecosologic.Api/appsettings.Development.json`
- Test: `backend/tests/Ecosologic.Api.Tests/CrmNotificationJobTests.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/HangfireJobRegistrationTests.cs`

**Interfaces:**
- `CrmNotificationJob.ExecuteAsync(CancellationToken)` resolves `ICrmNotificationSync` from a scope and calls `SyncAsync`.
- `HangfireJobRegistration.RegisterRecurringJobs(IRecurringJobManager, IConfiguration)` registers `crm-notifications` with the configured five-minute cron.

- [ ] **Step 1: Write the failing tests**

Test `HangfireJobRegistrationTests` must assert that registration creates exactly one recurring job with ID `crm-notifications` and a five-minute schedule. Test `CrmNotificationJob` with a real scoped service fake and assert that it executes once, propagates cancellation, and does not swallow exceptions so Hangfire can retry them.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter "FullyQualifiedName~HangfireJobRegistrationTests|FullyQualifiedName~CrmNotificationJob"`

Expected: FAIL because Hangfire packages, registration, and job do not exist.

- [ ] **Step 3: Add Hangfire packages and minimal registration**

Add `Hangfire.AspNetCore` to the API project and `Hangfire.PostgreSql` to the Infrastructure project using compatible stable versions. Configure `AddHangfire` with PostgreSQL storage, a dedicated `hangfire` schema, and `AddHangfireServer`. Register the recurring job once during startup using the stable ID and `Crm:NotificationCron` defaulting to `*/5 * * * *`.

- [ ] **Step 4: Implement the CRM job**

Move the worker's scoped resolution and service call into `CrmNotificationJob`. Do not catch general exceptions in the job; Hangfire must mark the job failed and apply retry policy. Keep cancellation passed to `SyncAsync`.

- [ ] **Step 5: Remove the old worker configuration**

Remove `AddHostedService<CrmNotificationWorker>()`, `CrmNotificationInterval`, the old loop registration, and the worker/loop files. Keep `CrmNotificationService` and all existing notification behavior unchanged.

- [ ] **Step 6: Run the focused and full tests**

Run: `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter "FullyQualifiedName~CrmNotification|FullyQualifiedName~Hangfire"`

Expected: PASS, including existing deduplication, retry-after-database-conflict, cancellation, and scope tests.

- [ ] **Step 7: Commit the isolated migration**

Run: `git add backend/src/Ecosologic.Api backend/src/Ecosologic.Infrastructure backend/tests/Ecosologic.Api.Tests && git commit -m "feat: migrate crm notifications to hangfire"`

---

### Task 2: Secure the Hangfire Dashboard and define operational configuration

**Files:**
- Create: `backend/src/Ecosologic.Api/Security/HangfireAuthorizationFilter.cs`
- Modify: `backend/src/Ecosologic.Api/Program.cs:88-106`
- Modify: `backend/src/Ecosologic.Api/appsettings.json`
- Modify: `backend/src/Ecosologic.Api/appsettings.Development.json`
- Modify: `docker-compose.server.yml`
- Create: `backend/tests/Ecosologic.Api.Tests/HangfireDashboardAuthorizationTests.cs`

**Interfaces:**
- `HangfireAuthorizationFilter` implements `IDashboardAuthorizationFilter` and accepts only an authenticated principal with role `Admin`.
- Dashboard route: `/hangfire`.

- [ ] **Step 1: Write the failing authorization tests**

Cover unauthenticated, authenticated non-admin, and authenticated admin principals. Assert only the admin request returns `true`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter FullyQualifiedName~HangfireDashboardAuthorizationTests`

Expected: FAIL because the filter is not defined.

- [ ] **Step 3: Implement authorization and dashboard mapping**

Map `/hangfire` after authentication and authorization middleware with the custom filter. Configure dashboard options to disable command execution for non-admin contexts and do not expose the route outside the normal authenticated pipeline.

- [ ] **Step 4: Add configuration defaults and production variables**

Add `Hangfire:StorageSchema`, `Crm:NotificationCron`, `Tariffs:SyncCron`, `Tariffs:TimeZoneId`, `Tariffs:SourceUrl`, `Tariffs:HttpTimeoutSeconds`, and `Tariffs:MaxRetries`. Add production environment mappings without hard-coded secrets.

- [ ] **Step 5: Verify the focused tests and build**

Run: `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter "FullyQualifiedName~HangfireDashboardAuthorizationTests|FullyQualifiedName~HangfireJobRegistrationTests"`

Expected: PASS and no build warnings caused by the dashboard integration.

- [ ] **Step 6: Commit the security/configuration change**

Run: `git add backend/src/Ecosologic.Api docker-compose.server.yml backend/tests/Ecosologic.Api.Tests && git commit -m "feat: secure hangfire dashboard"`

---

### Task 3: Create ANEEL source adapter and normalization

**Files:**
- Create: `backend/src/Ecosologic.Application/Solar/IAneelTariffSource.cs`
- Create: `backend/src/Ecosologic.Application/Solar/AneelTariffRecord.cs`
- Create: `backend/src/Ecosologic.Application/Solar/AneelTariffSyncOptions.cs`
- Create: `backend/src/Ecosologic.Infrastructure/Solar/AneelTariffClient.cs`
- Create: `backend/src/Ecosologic.Infrastructure/Solar/AneelTariffNormalizer.cs`
- Modify: `backend/src/Ecosologic.Infrastructure/Ecosologic.Infrastructure.csproj`
- Test: `backend/tests/Ecosologic.Api.Tests/AneelTariffNormalizerTests.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/AneelTariffClientTests.cs`
- Test fixture: `backend/tests/Ecosologic.Api.Tests/Fixtures/aneel-tariffs-light-enel-b.json`

**Interfaces:**
- `IAneelTariffSource.FetchAsync(AneelTariffSyncOptions, CancellationToken)` returns raw source records and a source hash.
- `AneelTariffNormalizer.Normalize(IEnumerable<AneelTariffRecord>, AneelTariffSyncOptions)` returns validated `TariffProfile`/`TariffComponent` input records.

- [ ] **Step 1: Capture a representative ANEEL fixture**

Capture the Power BI response or exported data corresponding to the approved filters and store only non-secret tariff data in the test fixture. Record the exact source URL and fields used. The fixture must include accepted Light/Enel B1/B2/B3 Fio B rows plus rejected distributor, subgroup, component, and unit rows.

- [ ] **Step 2: Write failing normalization tests**

Assert exact distributor mapping, B1/B2/B3 filtering, rejection of B4/group A/other components, preservation of source `R$/MWh`, conversion to the internal calculation unit, vigency parsing, and rejection of missing source or malformed values.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter FullyQualifiedName~AneelTariffNormalizerTests`

Expected: FAIL because the source contracts and normalizer do not exist.

- [ ] **Step 4: Implement typed source records and normalizer**

Implement exact-name matching for `Light Serviços de Eletricidade S.A.` and `Enel Distribuição Rio`, canonicalize accents and whitespace only for matching, retain the original display name, accept only the configured subgroups and Fio B component, parse decimal values using invariant culture, and convert `R$/MWh` to `R$/kWh` only for internal consumers.

- [ ] **Step 5: Write the client contract tests**

Use a fake `HttpMessageHandler` to assert configured URL, timeout, response status handling, malformed payload handling, and deterministic SHA-256 hash generation. Never include access tokens or raw sensitive responses in exception messages.

- [ ] **Step 6: Implement the ANEEL client**

Use `IHttpClientFactory` and a typed client. Encapsulate the Power BI/report protocol in the adapter; the rest of the application must receive typed records only. Return a failed result for non-success HTTP responses or schema mismatch.

- [ ] **Step 7: Run focused tests and commit**

Run: `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter "FullyQualifiedName~AneelTariffNormalizerTests|FullyQualifiedName~AneelTariffClientTests"`

Expected: PASS.

Run: `git add backend/src/Ecosologic.Application backend/src/Ecosologic.Infrastructure backend/tests/Ecosologic.Api.Tests && git commit -m "feat: add aneel tariff source adapter"`

---

### Task 4: Add import audit and transactional tariff reconciliation

**Files:**
- Modify: `backend/src/Ecosologic.Infrastructure/Persistence/EcosologicDbContext.cs`
- Create: `backend/src/Ecosologic.Infrastructure/Solar/AneelTariffImportService.cs`
- Create: `backend/src/Ecosologic.Infrastructure/Solar/AneelTariffImportModels.cs`
- Create: `backend/src/Ecosologic.Infrastructure/Persistence/Migrations/20260903_AddAneelTariffImports.cs`
- Modify: `backend/src/Ecosologic.Infrastructure/Solar/TariffCatalog.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/AneelTariffImportServiceTests.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/TariffCatalogTests.cs`

**Interfaces:**
- `AneelTariffImportService.ImportAsync(CancellationToken)` creates an import run, normalizes source data, reconciles both distributors atomically per batch, and completes/fails the run.
- `TariffCatalog.ReconcileImportedProfilesAsync(...)` closes prior versions and inserts changed immutable versions without overlap.

- [ ] **Step 1: Write failing audit and reconciliation tests**

Cover first import, identical rerun, changed source hash, validity rollover, incomplete data, rollback when one distributor fails, and preservation of the previous complete version.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter FullyQualifiedName~AneelTariffImportServiceTests`

Expected: FAIL because import audit storage and reconciliation do not exist.

- [ ] **Step 3: Add persistence records and migration**

Add `AneelTariffImportRecord` with run ID, Hangfire job ID, filters JSON, source URL, source hash, timestamps, status, counts, and sanitized error. Add indexes for status/start time and source hash. Keep tariff profile/component records immutable from the import service perspective.

- [ ] **Step 4: Implement reconciliation**

Use a serializable transaction. Match by distributor, group, subgroup, modality, post, component kind, and effective source identity. Treat equal normalized values, validity, and source hash as no-op. For changed data, set the previous end date to the new start date minus the smallest supported interval and insert the new version. Reject overlaps before commit.

- [ ] **Step 5: Implement import status transitions**

Create `Running` before fetching, update counters after validation, set `Succeeded` only after the tariff transaction commits, and set `Failed` with a sanitized error on any exception. Ensure a failed run leaves existing tariff rows untouched.

- [ ] **Step 6: Run focused, tariff, and persistence tests**

Run: `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter "FullyQualifiedName~AneelTariffImportServiceTests|FullyQualifiedName~TariffCatalogTests"`

Expected: PASS, including existing overlap and completeness invariants.

- [ ] **Step 7: Commit persistence and reconciliation**

Run: `git add backend/src/Ecosologic.Infrastructure backend/tests/Ecosologic.Api.Tests && git commit -m "feat: reconcile aneel tariffs with audit history"`

---

### Task 5: Schedule ANEEL sync and add Admin control API

**Files:**
- Create: `backend/src/Ecosologic.Infrastructure/Jobs/AneelTariffSyncJob.cs`
- Modify: `backend/src/Ecosologic.Infrastructure/Jobs/HangfireJobRegistration.cs`
- Modify: `backend/src/Ecosologic.Api/Program.cs:47-62`
- Create: `backend/src/Ecosologic.Api/Controllers/AdminTariffsController.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/AneelTariffSyncJobTests.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/AdminTariffsControllerTests.cs`

**Interfaces:**
- `AneelTariffSyncJob.ExecuteAsync(CancellationToken)` calls `AneelTariffImportService.ImportAsync`.
- `POST /api/admin/tariffs/sync` enqueues `AneelTariffSyncJob` and returns `202 Accepted` with the import run ID.
- `GET /api/admin/tariffs/sync/status` returns the latest import status and counters.

- [ ] **Step 1: Write failing job and API tests**

Assert recurring ID `aneel-tariff-sync`, monthly cron from configuration, Admin-only access, no synchronous network call in the controller, and status response fields.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter "FullyQualifiedName~AneelTariffSyncJobTests|FullyQualifiedName~AdminTariffsControllerTests"`

Expected: FAIL because the job and controller do not exist.

- [ ] **Step 3: Implement the job and recurring registration**

Register `aneel-tariff-sync` with a configurable cron defaulting to `0 3 1 * *` and timezone `America/Sao_Paulo`. Apply Hangfire retry policy with bounded attempts and exponential backoff. Add a distributed job lock so manual and scheduled runs cannot overlap.

- [ ] **Step 4: Implement the Admin endpoints**

Use `[Authorize(Roles = "Admin")]`. The POST endpoint creates a pending import run and enqueues the job with its ID; it accepts no tariff values. The GET endpoint reads the latest run with `AsNoTracking` and returns `404` when no run exists.

- [ ] **Step 5: Run focused and full tests**

Run: `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter "FullyQualifiedName~AneelTariffSyncJobTests|FullyQualifiedName~AdminTariffsControllerTests"`

Expected: PASS.

Run: `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj`

Expected: PASS with all existing tests green.

- [ ] **Step 6: Commit scheduling and API**

Run: `git add backend/src backend/tests && git commit -m "feat: schedule monthly aneel tariff sync"`

---

### Task 6: Production validation and remove legacy artifacts

**Files:**
- Modify: `ops/production.md`
- Modify: `docker-compose.server.yml`
- Modify: `docs/superpowers/specs/2026-09-03-hangfire-aneel-sync-design.md`
- Modify: `docs/superpowers/plans/2026-09-03-hangfire-aneel-sync.md`
- Test: `backend/tests/Ecosologic.Api.Tests/CrmNotificationWorkerTests.cs`

- [ ] **Step 1: Remove obsolete tests and configuration references**

Replace tests named for the deleted worker with Hangfire job tests while preserving assertions for execution, exception recovery through Hangfire retries, deduplication, scoped service resolution, and cancellation. Search the solution for `AddHostedService<CrmNotificationWorker>`, `CrmNotificationLoop`, and `CrmNotificationInterval`; expected result is no production reference.

- [ ] **Step 2: Build and run the complete test suite**

Run: `dotnet build backend/Ecosologic.sln --configuration Release`

Expected: build succeeds without errors.

Run: `dotnet test backend/Ecosologic.sln --configuration Release --no-build`

Expected: all tests pass.

- [ ] **Step 3: Apply and validate PostgreSQL migrations remotely**

Deploy the build to the production host using the existing operational procedure, let the application migration runner apply the Hangfire/import migrations, and verify the Hangfire schema, recurring jobs, and import audit table exist. Do not expose database credentials in logs.

- [ ] **Step 4: Validate operational behavior**

Open `/hangfire` as Admin and verify both recurring IDs. Trigger the CRM job and confirm notification deduplication. Trigger `/api/admin/tariffs/sync`, verify `Running` then `Succeeded` or a retained failure, and confirm only the approved distributors/subgroups/components were imported.

- [ ] **Step 5: Validate failure safety**

Temporarily force the ANEEL adapter to return an invalid response in a non-production test environment. Confirm Hangfire retries, the import is marked `Failed`, and the previous tariff version remains eligible.

- [ ] **Step 6: Update operations documentation**

Document Hangfire storage configuration, Dashboard route and role, recurring job IDs, monthly schedule, manual sync endpoint, status interpretation, retry behavior, and recovery procedure. Record that the old BackgroundService is no longer used.
