# Supplier Deduplication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tornar a importação de fornecedores idempotente e compatível com fontes longas.

**Architecture:** A API será responsável pela deduplicação definitiva, usando nomes normalizados e consulta ao banco antes de inserir. O workflow manterá o limite de busca atual e enviará candidatos; a restrição única do PostgreSQL permanece como proteção concorrente.

**Tech Stack:** ASP.NET Core, EF Core, PostgreSQL, n8n JSON workflow, xUnit.

## Global Constraints

- Não sobrescrever fornecedores existentes.
- Não exibir ou versionar segredos.
- Manter a coluna `Source` compatível com URLs de até 512 caracteres.
- Executar testes focados antes do build/teste completo.

---

### Task 1: Add regression tests

**Files:**
- Modify: `backend/tests/Ecosologic.Api.Tests/` using the existing API test conventions.
- Test: supplier import behavior for duplicate and existing names.

- [ ] Add a failing API test that submits `Solar`, `solar`, and ` Solar ` and expects one inserted record.
- [ ] Add a failing API test that preloads an existing supplier and expects a repeated import not to create another row.
- [ ] Add a failing persistence/model test for a source URL longer than 64 characters.
- [ ] Run the focused tests and confirm they fail for the intended reason.

### Task 2: Implement idempotent import

**Files:**
- Modify: `backend/src/Ecosologic.Api/Controllers/SolarSuppliersController.cs`.
- Modify: `backend/src/Ecosologic.Infrastructure/Persistence/SolarSupplierRecord.cs`.
- Modify: `backend/src/Ecosologic.Infrastructure/Persistence/EcosologicDbContext.cs` if the active model has duplicate configuration.

- [ ] Normalize candidate names with trim and case-insensitive comparison.
- [ ] Remove duplicate names within the request while preserving first occurrence.
- [ ] Query existing names and add only absent suppliers.
- [ ] Return only newly inserted records.
- [ ] Change `Source` max length from 64 to 512.

### Task 3: Add schema migration

**Files:**
- Create: `backend/src/Ecosologic.Infrastructure/Persistence/Migrations/<timestamp>_ExpandSolarSupplierSource.cs`.

- [ ] Generate a migration that alters `solar_suppliers.Source` to `varchar(512)`.
- [ ] Build the solution and verify the migration compiles.
- [ ] Apply the migration to the production database without dropping data.

### Task 4: Align n8n workflow and documentation

**Files:**
- Modify: `ops/n8n-supplier-discovery-workflow.json`.
- Modify: `ops/n8n-supplier-discovery-runbook.md`.

- [ ] Tell the normalization prompt to return unique suppliers by normalized name.
- [ ] Document current search page limits and API-level idempotency.
- [ ] Document that reruns preserve existing records and insert only new names.

### Task 5: Verify production execution

- [ ] Publish the workflow.
- [ ] Execute it in an isolated n8n CLI process.
- [ ] Confirm success and verify database count and unique normalized names.
- [ ] Run the workflow a second time and confirm the count does not increase.
