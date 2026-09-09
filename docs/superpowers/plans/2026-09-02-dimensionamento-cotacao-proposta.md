# Dimensionamento, Cotacao e Proposta Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven development or executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Transformar leads em dimensionamentos tecnicos, cotacoes comerciais e propostas internas versionadas.

**Architecture:** O calculo sera um motor deterministico separado da API HTTP, com entradas normalizadas, tarifas versionadas e snapshots imutaveis por dimensionamento/cotacao/proposta. Light RJ e Enel RJ serao perfis de tarifa; nenhuma regra tarifaria ou Fio B ficara espalhada no frontend.

**Tech Stack:** .NET 9, EF Core 9, PostgreSQL, Angular 20, TypeScript, ImageSharp/PDF template versionado.

## Global Constraints

- O resultado e interno; nao sera exibido diretamente ao cliente.
- Suportar grupos A e B, modalidades e postos tarifarios aplicaveis.
- Suportar Light RJ e Enel Distribuicao Rio.
- Fio B deve ser parametrizado por concessionaria, vigencia, ano e posto.
- Toda proposta deve preservar snapshot dos parametros usados.
- Bloquear emissao quando faltar regra tarifaria vigente.
- Usar testes de regressao baseados na planilha, sem transformar seus valores em defaults.
- Validar unidades, premissas, fontes e vigencia na interface.

---

### Task 1: Modelos e snapshots

**Files:**
- Create: `backend/src/Ecosologic.Domain/Solar/SolarSizing.cs`
- Create: `backend/src/Ecosologic.Domain/Solar/SolarQuote.cs`
- Create: `backend/src/Ecosologic.Domain/Solar/Proposal.cs`
- Modify: `backend/src/Ecosologic.Infrastructure/Persistence/EcosologicDbContext.cs`
- Create: `backend/src/Ecosologic.Infrastructure/Persistence/Migrations/<timestamp>_AddSolarCommercialModels.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/SolarCommercialModelsTests.cs`

**Deliverable:** entidades com status, relacionamentos, dados de entrada/resultados e JSON snapshot versionado; migration aplicada somente apos testes de persistencia.

### Task 2: Perfis tarifarios

**Files:**
- Create: `backend/src/Ecosologic.Domain/Solar/TariffProfile.cs`
- Create: `backend/src/Ecosologic.Domain/Solar/TariffComponent.cs`
- Create: `backend/src/Ecosologic.Infrastructure/Solar/TariffCatalog.cs`
- Create: `backend/src/Ecosologic.Api/Controllers/TariffsController.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/TariffCatalogTests.cs`

**Deliverable:** catalogo versionado para Light/Enel, grupos A/B, modalidades, postos, disponibilidade e componentes TE/TUSD/Fio B; endpoint Admin somente leitura para selecao.

### Task 3: Motor tecnico

**Files:**
- Create: `backend/src/Ecosologic.Domain/Solar/SolarSizingInput.cs`
- Create: `backend/src/Ecosologic.Domain/Solar/SolarSizingResult.cs`
- Create: `backend/src/Ecosologic.Application/Solar/SolarSizingCalculator.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/SolarSizingCalculatorTests.cs`

**Deliverable:** geracao mensal/anual, HSP/K, perdas, kWp, modulos, inversor, strings, cobertura, deficit/excedente e alertas; incluir regressao do caso de referencia da planilha.

### Task 4: Fio B e fatura

**Files:**
- Create: `backend/src/Ecosologic.Application/Solar/TariffBillCalculator.cs`
- Create: `backend/src/Ecosologic.Application/Solar/FioBCalculator.cs`
- Modify: `backend/src/Ecosologic.Api/Controllers/TariffsController.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/FioBCalculatorTests.cs`

**Deliverable:** calculo por posto e grupo, disponibilidade, energia compensavel, Fio B progressivo e tributos; ausencia de perfil vigente retorna erro de validacao.

### Task 5: API e tela interna de dimensionamento

**Files:**
- Create: `backend/src/Ecosologic.Api/Controllers/SolarSizingController.cs`
- Create: `frontend/src/app/solar-sizing.service.ts`
- Create: `frontend/src/app/admin/solar-sizing.ts`
- Create: `frontend/src/app/admin/solar-sizing.html`
- Create: `frontend/src/app/admin/solar-sizing.scss`
- Modify: `frontend/src/app/app.routes.ts`
- Test: backend controller tests and Angular service/component tests

**Deliverable:** wizard interno em seis etapas, rascunho, premissas, alertas e resultados tecnicos sem exposicao publica.

### Task 6: Cotacao

**Files:**
- Create: `backend/src/Ecosologic.Api/Controllers/SolarQuotesController.cs`
- Create: `frontend/src/app/admin/solar-quote.ts`
- Create: `frontend/src/app/admin/solar-quote.html`
- Create: `frontend/src/app/admin/solar-quote.scss`
- Test: backend and Angular quote tests

**Deliverable:** itens, equipamentos, servicos, frete, custo, margem, impostos, preco, pagamento, validade e auditoria de alteracoes.

### Task 7: Template e proposta PDF

**Files:**
- Create: `backend/src/Ecosologic.Application/Solar/ProposalRenderer.cs`
- Create: `backend/src/Ecosologic.Api/Controllers/ProposalsController.cs`
- Create: `frontend/src/app/admin/proposal-preview.ts`
- Create: `frontend/src/app/admin/proposal-preview.html`
- Modify: `ops/production.md`
- Test: renderer snapshot tests and API tests

**Deliverable:** template versionado com campos de cliente, sistema, geracao, economia, equipamentos, preco, garantias e condicoes; gerar PDF sem alterar snapshots anteriores.

### Task 8: E2E comercial

**Files:**
- Create: `frontend/e2e/solar-commercial.spec.ts`
- Modify: `frontend/playwright.config.ts`
- Test: production-like seeded flow

**Deliverable:** fluxo E2E Admin `Lead -> Dimensionamento -> Cotacao -> Proposta`, validacao de bloqueios, PDF e historico.

## Verification Commands

```bash
dotnet test Ecosologic.sln
dotnet build Ecosologic.sln
cd frontend
npx ng test --watch=false --browsers=ChromeHeadless
npm run build
npx playwright test
```

## Known Inputs To Confirm During Tasks

- Tarifas oficiais vigentes de Light RJ e Enel RJ.
- Regras regulatorias aplicaveis ao enquadramento e data de conexao.
- Exportacao PDF ou IDML do arquivo INDD para fidelidade visual.
- Condicoes comerciais, garantias, prazos e formas de pagamento.
