# Dimensionamento Comercial Sistemico Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transformar dados de fatura, materiais e regras tarifarias atuais em dimensionamento, graficos e proposta PDF dentro do CRM, sem gerar arquivo Excel.

**Architecture:** O backend sera a unica fonte dos calculos. O catalogo persistira modulos, inversores e historico de precos; cada dimensionamento preservara um snapshot tecnico, enquanto precos somente serao atualizados por acao explicita. O frontend exibira o formulario e os resultados; o renderer PDF consumira o mesmo resultado calculado e os mesmos pontos de grafico.

**Tech Stack:** .NET 9, EF Core 9, PostgreSQL, Angular 20, TypeScript, HTML/CSS PDF renderer, xUnit, Playwright.

## Global Constraints

- Nao gerar arquivo Excel; `PlanilhaDimensionamento750.xlsx` sera somente referencia de regressao.
- Receber exatamente 12 consumos mensais em kWh e 12 valores mensais da fatura.
- Receber marca, modelo e potencia de modulo e inversor em campos livres.
- Dados tecnicos encontrados na internet so entram no catalogo apos confirmacao do usuario.
- Materiais confirmados sao reutilizaveis em novos orcamentos.
- Precos antigos so mudam com a acao explicita `Atualizar precos/recalcular orcamento`.
- PDFs ja emitidos permanecem imutaveis ate regeneracao manual.
- Tarifas, Fio B, disponibilidade, creditos e tributos devem ser versionados por vigencia e fonte.
- Graficos devem ser calculados no backend e exibidos no CRM e no PDF.
- Nao usar `geracao x tarifa cheia` como economia.
- Bloquear emissao se faltar regra tarifaria vigente ou houver alerta tecnico impeditivo.

---

### Task 1: Catalogo de materiais e precos

**Files:**
- Create: `backend/src/Ecosologic.Domain/Solar/SolarMaterial.cs`
- Create: `backend/src/Ecosologic.Domain/Solar/SolarMaterialPrice.cs`
- Modify: `backend/src/Ecosologic.Infrastructure/Persistence/EcosologicDbContext.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/SolarMaterialTests.cs`

**Interfaces:**
- `SolarMaterial.Create(type, brand, model, powerW, technicalDataJson, sourceUrl)` retorna material validado.
- `SolarMaterial.AddPrice(amount, validFrom)` cria historico sem apagar precos anteriores.
- `SolarMaterial.CurrentPrice(asOf)` retorna o preco vigente na data.

- [ ] Escrever testes para normalizacao de marca/modelo, potencia positiva, preco vigente e reuso de material.
- [ ] Rodar `dotnet test backend/tests/Ecosologic.Api.Tests/Ecosologic.Api.Tests.csproj --filter "FullyQualifiedName~SolarMaterialTests"` e confirmar falha inicial.
- [ ] Implementar entidades, indices por tipo/marca/modelo e restricao de preco nao negativo.
- [ ] Criar a migration com `dotnet ef migrations add AddSolarMaterials --project backend/src/Ecosologic.Infrastructure --startup-project backend/src/Ecosologic.Api` e nao aplica-la em producao nesta etapa.
- [ ] Rodar os testes focados e confirmar aprovacao.

### Task 2: Fatura mensal e snapshot do dimensionamento

**Files:**
- Modify: `backend/src/Ecosologic.Domain/Solar/SolarSizing.cs`
- Modify: `backend/src/Ecosologic.Infrastructure/Persistence/EcosologicDbContext.cs`
- Create: `backend/src/Ecosologic.Application/Solar/SolarSizingRequest.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/SolarCommercialModelsTests.cs`

**Interfaces:**
- `SolarSizingRequest` tera `MonthlyConsumptionKWh[12]`, `MonthlyBillAmount[12]`, distribuidora, grupo, modalidade, ligacao, material ids, endereco e premissas.
- `SolarSizingRecord` armazenara `InputsJson`, `ResultJson`, `MaterialSnapshotJson`, `TariffSnapshotJson` e `EngineVersion`.

- [ ] Escrever testes exigindo 12 consumos e 12 valores de fatura, aceitando zero somente onde a regra tarifaria permitir.
- [ ] Implementar validacao de moeda, kWh, data de protocolo e snapshot imutavel do material.
- [ ] Associar o dimensionamento ao lead existente e manter transicoes Draft/Calculated/Approved/Cancelled.
- [ ] Rodar testes de dominio e persistencia focados.

### Task 3: Regras tarifarias atuais

**Files:**
- Modify: `backend/src/Ecosologic.Application/Solar/FioBCalculator.cs`
- Modify: `backend/src/Ecosologic.Application/Solar/TariffBillCalculator.cs`
- Modify: `backend/src/Ecosologic.Infrastructure/Solar/TariffCatalog.cs`
- Modify: `backend/src/Ecosologic.Api/Controllers/TariffsController.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/FioBCalculatorTests.cs`

**Interfaces:**
- `TariffBillCalculator.Calculate(TariffBillInput input)` retorna fatura sem solar, fatura com solar, economia, disponibilidade, Fio B e linhas por posto.
- `FioBCalculator.Calculate(FioBRequest request)` seleciona percentual pela data de referencia, distribuidora, grupo, modalidade e excecoes legais.

- [ ] Adicionar fixtures oficiais versionadas para Light RJ e Enel Rio com URL, vigencia e data de revisao.
- [ ] Testar Fio B de 60% em 2026, escalonamento posterior, direito adquirido, custo de disponibilidade e creditos de 60 meses.
- [ ] Testar que bandeira incide apenas sobre consumo faturado e que Grupo A separa demanda e postos.
- [ ] Bloquear regra ausente, ambigua ou sobreposta para a mesma combinacao e ano.
- [ ] Executar os testes tarifarios antes de alterar o frontend.

### Task 4: Calculo mensal e series para graficos

**Files:**
- Modify: `backend/src/Ecosologic.Application/Solar/SolarSizingCalculator.cs`
- Create: `backend/src/Ecosologic.Domain/Solar/SolarChartSeries.cs`
- Modify: `backend/src/Ecosologic.Domain/Solar/SolarSizingResult.cs`
- Test: `backend/tests/Ecosologic.Api.Tests/SolarSizingCalculatorTests.cs`

**Interfaces:**
- `SolarSizingCalculator.Calculate(SolarSizingInput input)` retorna resultado tecnico com 12 pontos mensais.
- `SolarSizingResult.Charts` contem `GenerationVsConsumption`, `FinancialCashFlow` e `AnnualBillComparison`.
- Cada ponto possui `label`, `value` e `unit`; o frontend nao recalcula valores financeiros.

- [ ] Escrever regressao do caso de 750 kWh usando os 12 consumos da planilha.
- [ ] Escrever testes para mes pior, excedente, deficit, cobertura, geração anual e alertas eletricos.
- [ ] Adicionar series calculadas com o mesmo engine versionado do resultado.
- [ ] Validar que nenhum valor NaN/Infinity chega ao contrato da API.
- [ ] Rodar `dotnet test Ecosologic.sln --filter "FullyQualifiedName~SolarSizingCalculatorTests"`.

### Task 5: API e formulario CRM

**Files:**
- Create: `backend/src/Ecosologic.Api/Controllers/SolarSizingController.cs`
- Create: `backend/src/Ecosologic.Api/Contracts/SolarSizingContracts.cs`
- Create: `frontend/src/app/solar-sizing.service.ts`
- Create: `frontend/src/app/admin/solar-sizing.ts`
- Create: `frontend/src/app/admin/solar-sizing.html`
- Create: `frontend/src/app/admin/solar-sizing.scss`
- Modify: `frontend/src/app/app.routes.ts`
- Test: controller tests and Angular component tests

- [ ] Criar endpoint autenticado para catalogo, consulta por modelo e salvar rascunho.
- [ ] Criar wizard com 12 meses de consumo, 12 valores de conta, equipamentos e premissas.
- [ ] Exibir resultados, alertas e graficos sem duplicar calculo no TypeScript.
- [ ] Adicionar acao explicita `Atualizar precos/recalcular orcamento`.
- [ ] Testar validacao, carregamento de material existente e comportamento mobile.

### Task 6: Pesquisa e confirmacao de material

**Files:**
- Create: `backend/src/Ecosologic.Application/Solar/SolarMaterialLookup.cs`
- Create: `backend/src/Ecosologic.Api/Controllers/SolarMaterialsController.cs`
- Modify: `frontend/src/app/solar-sizing.service.ts`
- Modify: `frontend/src/app/admin/solar-sizing.ts`
- Test: lookup and confirmation tests

- [ ] Implementar busca por marca/modelo em catalogo local primeiro.
- [ ] Permitir registrar URL/fonte e dados tecnicos confirmados pelo operador.
- [ ] Nao gravar resultado externo automaticamente nem prometer compatibilidade sem alerta.
- [ ] Ao confirmar, persistir os campos necessarios ao calculo eletrico do modulo/inversor.
- [ ] Testar que orcamentos anteriores continuam com snapshot tecnico original.

### Task 7: Cotacao, precos e regeneracao

**Files:**
- Modify: `backend/src/Ecosologic.Domain/Solar/SolarQuote.cs`
- Modify: `backend/src/Ecosologic.Infrastructure/Persistence/EcosologicDbContext.cs`
- Create/Modify: `backend/src/Ecosologic.Api/Controllers/SolarQuotesController.cs`
- Create: `frontend/src/app/admin/solar-quote.ts`
- Test: quote and price refresh tests

- [ ] Criar itens comerciais com material, quantidade, preco snapshot e preco atual.
- [ ] Implementar recalculo explicito que atualiza precos e refaz resultados financeiros.
- [ ] Manter status e auditoria de quem executou a atualizacao.
- [ ] Confirmar que consultar o orcamento nao altera valores.
- [ ] Testar atualizacao de preco, regeneracao e preservacao do PDF antigo.

### Task 8: PDF com graficos e imagens

**Files:**
- Create/Modify: `backend/src/Ecosologic.Application/Solar/ProposalRenderer.cs`
- Create/Modify: `backend/src/Ecosologic.Api/Controllers/ProposalsController.cs`
- Create: `frontend/src/app/admin/proposal-preview.ts`
- Create: `frontend/src/app/admin/proposal-preview.html`
- Create: `frontend/src/app/admin/proposal-preview.scss`
- Test: renderer snapshot and API tests

- [ ] Reproduzir a estrutura visual do `docs/superpowers/specs/Orcamento750kwh_Eliezer.pdf`.
- [ ] Renderizar tabela de equipamentos e dados tecnicos confirmados.
- [ ] Renderizar graficos de geracao/consumo, financeiro acumulado e comparativo de contas.
- [ ] Inserir fotos com fonte registrada, sem depender de URL externa durante a geracao.
- [ ] Renderizar observacoes atualizadas sobre Fio B, tarifa, variacao de geracao, visita tecnica e validade.
- [ ] Gerar hash e versao do PDF; nova versao somente apos acao explicita.
- [ ] Testar PDF com caso de 750 kWh e bloquear geracao com alertas impeditivos.

### Task 9: E2E comercial e verificacao final

**Files:**
- Create: `frontend/e2e/solar-commercial.spec.ts`
- Modify: `frontend/playwright.config.ts`
- Modify: `docs/superpowers/specs/2026-09-02-dimensionamento-cotacao-design.md`

- [ ] Cobrir fluxo Admin `Lead -> Fatura -> Material -> Dimensionamento -> Graficos -> Cotacao -> PDF`.
- [ ] Cobrir atualizacao manual de preco sem alteracao automatica.
- [ ] Cobrir bloqueio por regra tarifaria ausente e incompatibilidade eletrica.
- [ ] Rodar `dotnet test Ecosologic.sln`.
- [ ] Rodar `dotnet build Ecosologic.sln`.
- [ ] Rodar `npm run build` em `frontend`.
- [ ] Rodar `npm run e2e` em `frontend`.
