# Ecosologic Site + CRM Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an Angular/Ionic Ecosologic website with editable content, a small CRM, on-grid sizing, RJ tariffs and versioned commercial PDF proposals.

**Architecture:** Use a modular monolith with ASP.NET Core .NET 9, Clean Architecture and PostgreSQL. Keep calculation rules in the Domain/Application layers, expose them through REST, and use Angular/Ionic for separate public and authenticated admin surfaces.

**Tech Stack:** Angular, Ionic, TypeScript, ASP.NET Core .NET 9, EF Core, PostgreSQL, JWT, Swagger/OpenAPI, HTML/CSS PDF rendering, xUnit, Playwright.

## Global Constraints

- Preserve `images/logo.png` and `images/logo-favicon.png` from `C:\Projetos\ecosologicHospedado`.
- Use `fabio@ecosologic.com.br` and `+55 (21) 99542-4027` as the initial contact settings.
- Restrict tariff catalog scope to Rio de Janeiro, Light and Enel, with municipality mapping and manual override.
- Store tariff source, validity, Fio B rule version and calculation inputs with every result.
- Label sizing and savings as preliminary estimates requiring technical validation.
- Meet WCAG 2.2 AA basics and honor `prefers-reduced-motion`.

### Task 1: Workspace Foundation

**Files:**
- Create: `frontend/` Angular/Ionic workspace files
- Create: `backend/src/Ecosologic.Api/`
- Create: `backend/src/Ecosologic.Domain/`
- Create: `backend/src/Ecosologic.Application/`
- Create: `backend/src/Ecosologic.Infrastructure/`
- Create: `backend/tests/`
- Create: `docker-compose.yml`

- [ ] Create the Angular/Ionic app and .NET solution with projects for Domain, Application, Infrastructure and Api.
- [ ] Configure PostgreSQL connection by environment variable and add EF Core migrations.
- [ ] Add health endpoints and a minimal public shell.
- [ ] Run `npm test`, `dotnet test backend/tests`, and `dotnet build backend/Ecosologic.sln`.

### Task 2: Public Brand Website

**Files:**
- Create: `frontend/src/app/pages/home/`
- Create: `frontend/src/app/components/`
- Create: `frontend/src/styles/tokens.scss`
- Modify: `frontend/src/index.html`
- Copy: `frontend/src/assets/brand/logo.png`, `frontend/src/assets/brand/favicon.png`

- [ ] Build Home, Services, Projects, Guide and Contact sections using typed Angular components.
- [ ] Reuse the supplied logo and favicon; do not redraw or replace them.
- [ ] Apply `DESIGN.md` tokens, responsive layouts, visible focus states and reduced-motion rules.
- [ ] Add a fixed WhatsApp action and a contact form posting to the API adapter.
- [ ] Add Playwright checks for desktop/mobile navigation, form labels and key CTA links.

### Task 3: Identity And CMS

**Files:**
- Create: `backend/src/Ecosologic.Domain/Content/`
- Create: `backend/src/Ecosologic.Application/Content/`
- Create: `backend/src/Ecosologic.Api/Controllers/ContentController.cs`
- Create: `frontend/src/app/admin/content/`

- [ ] Add entities for Page, SectionBlock, MediaAsset, Project, Testimonial and SiteSettings.
- [ ] Implement admin JWT login and CRUD endpoints with authorization.
- [ ] Implement draft, preview and published states; render only typed blocks on the public site.
- [ ] Add admin editing for text, images, ordering, visibility, contact settings and support media links.
- [ ] Test an edited homepage phrase and image through API and browser flow.

### Task 4: CRM Leads

**Files:**
- Create: `backend/src/Ecosologic.Domain/Crm/`
- Create: `backend/src/Ecosologic.Application/Crm/`
- Create: `backend/src/Ecosologic.Api/Controllers/LeadsController.cs`
- Create: `frontend/src/app/admin/crm/`

- [ ] Add Lead, LeadActivity, PipelineStage and CustomerSite entities.
- [ ] Accept public contact/quote submissions with validation and rate limiting.
- [ ] Implement stages: Novo lead, Em contato, Dados recebidos, Dimensionamento, Proposta enviada, Negociação, Fechado and Perdido.
- [ ] Build a responsive lead list, detail view, notes, reminders and stage changes.
- [ ] Test public submission, persistence, notification failure recovery and audit entries.

### Task 5: RJ Tariffs And Fio B

**Files:**
- Create: `backend/src/Ecosologic.Domain/Tariffs/`
- Create: `backend/src/Ecosologic.Application/Tariffs/`
- Create: `backend/src/Ecosologic.Infrastructure/Tariffs/`
- Create: `frontend/src/app/admin/tariffs/`

- [ ] Add Distributor, MunicipalityCoverage, TariffVersion and FioBRule entities.
- [ ] Seed Light and Enel municipality mappings only after validating the official coverage table.
- [ ] Store TUSD/TE/Fio B in source units and normalize `R$/MWh` to `R$/kWh` by dividing by 1,000.
- [ ] Seed configurable Fio B rules for 2023 through 2028 and model acquired-rights/definitive rules without hard-coding assumptions.
- [ ] Implement real bill tariff override, manual distributor override and expired-tariff warnings.
- [ ] Test municipality resolution, override precedence, unit conversion and Fio B year selection.

### Task 6: On-Grid Dimensioning

**Files:**
- Create: `backend/src/Ecosologic.Domain/Dimensioning/`
- Create: `backend/src/Ecosologic.Application/Dimensioning/`
- Create: `backend/src/Ecosologic.Api/Controllers/DimensioningController.cs`
- Create: `frontend/src/app/admin/dimensioning/`

- [ ] Implement a pure calculation service receiving consumption, tariff, HSP, losses, module power, inverter power and connection data.
- [ ] Return module count, kWp, inverter sizing, monthly/yearly generation, compensated energy, availability cost, bill estimate and savings.
- [ ] Reproduce the validated base formulas from `PlanilhaDimensionamento600.xlsx` before adding financial indicators.
- [ ] Persist immutable input snapshots, tariff snapshots and calculation rule versions.
- [ ] Add unit, boundary and regression tests using the supplied workbook values as fixtures.

### Task 7: Proposal PDF

**Files:**
- Create: `backend/src/Ecosologic.Application/Proposals/`
- Create: `backend/src/Ecosologic.Infrastructure/Proposals/ProposalPdfRenderer.cs`
- Create: `backend/src/Ecosologic.Api/Controllers/ProposalsController.cs`
- Create: `frontend/src/app/admin/proposals/`

- [ ] Create an HTML/CSS proposal template inspired by the supplied InDesign layout.
- [ ] Include customer, system, chart, Fio B savings, equipment, investment, payment terms, warranties, scope, assumptions, validity and contact data.
- [ ] Snapshot all values when issuing a proposal and prevent mutation of issued versions.
- [ ] Add download, email delivery and WhatsApp sharing links.
- [ ] Test PDF generation, missing-input blocking and version preservation.

### Task 8: ShinePhone Support Center

**Files:**
- Create: `frontend/src/app/pages/support/shinesphone-guide/`
- Create: `backend/src/Ecosologic.Domain/Content/SupportGuide.cs`
- Modify: `frontend/src/app/admin/content/`

- [ ] Add a mobile-first guide for Growatt MIN 5000 TL-X with ShinePhone app download links.
- [ ] Cover datalogger identification, 2.4 GHz setup, router/password replacement, reset/re-pairing, LED checks and escalation to WhatsApp.
- [ ] Require datalogger model selection before showing model-specific instructions.
- [ ] Support editable images, videos and text through CMS.
- [ ] Test guide navigation on mobile and verify external links are labeled.

### Task 9: Financial Expansion And Release Checks

**Files:**
- Modify: `backend/src/Ecosologic.Domain/Dimensioning/`
- Create: `backend/tests/Integration/`
- Create: `frontend/e2e/`

- [ ] Add payback, ROI, VPL, TIR and 25-year projections only after base on-grid regression fixtures pass.
- [ ] Add integration coverage for lead-to-proposal and public-content publishing.
- [ ] Run `dotnet test`, `npm test`, `npm run e2e`, accessibility checks and production builds.
- [ ] Verify backups, environment secrets, file validation, logs, rate limiting and error responses.
- [ ] Defer off-grid and hybrid algorithms to a separate validated milestone.

## Pendências: Upload de mídia

- [ ] **Quota de armazenamento por usuário/conta** — não implementada neste incremento. Exigiria infraestrutura adicional (contagem de bytes por remetente persistida no banco) além das políticas atuais de sanitização, limite de tamanho (5 MB), limite de dimensões/pixels (25 MP / 16384px) e rate limiting de upload. Aplicar quando a persistência de ativos existir como entidade (`MediaAsset`).

