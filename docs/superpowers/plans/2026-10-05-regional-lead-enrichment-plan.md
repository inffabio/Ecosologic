# Regional Lead Enrichment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Criar um webhook n8n que recebe cidades e enriquece leads do CRM com pesquisa pública do Brave.

**Architecture:** O n8n coordena entrada, busca, análise e escrita no CRM. O Brave fornece páginas públicas; o 9Router classifica evidências; a API existente de leads recebe notas, atividades e tarefas. Nenhum novo armazenamento é necessário.

**Tech Stack:** n8n, Brave Search API, 9Router, ASP.NET Core CRM API.

## Global Constraints

- Entrada por webhook autenticado.
- Máximo de 20 cidades por execução e 10 leads por cidade.
- Não acessar conteúdo privado ou exigir login em redes sociais.
- Preservar notas existentes.
- Não enviar mensagens automaticamente.

---

### Task 1: Map CRM contracts

- [ ] Confirm the exact lead list, patch, activity and task payloads in `LeadsController.cs`.
- [ ] Confirm the admin login response used by existing n8n workflows.

### Task 2: Build workflow

**File:** `ops/n8n-regional-lead-enrichment-workflow.json`

- [ ] Add authenticated webhook accepting `cities` and `maxLeadsPerCity`.
- [ ] Normalize and deduplicate city names.
- [ ] Fetch eligible CRM leads.
- [ ] Execute Brave queries per city with a hard result cap.
- [ ] Use 9Router to produce evidence-backed enrichment JSON.
- [ ] Match enrichment to CRM leads and update notes without duplicate blocks.
- [ ] Add activity and follow-up task only for newly changed leads.

### Task 3: Publish and verify

- [ ] Publish the workflow without activating it globally.
- [ ] Execute with Marica, Saquarema, Rio das Ostras and Macae.
- [ ] Verify city filtering, note content, activity and task creation.
- [ ] Repeat the same request and verify idempotency.

### Task 4: Document operations

- [ ] Add webhook payload, response shape, limits and troubleshooting to `ops/n8n-supplier-discovery-runbook.md` or a dedicated regional lead runbook.
- [ ] Document LGPD and public-data restrictions.
