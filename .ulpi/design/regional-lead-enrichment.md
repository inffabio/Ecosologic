# Prospecção Regional e Inteligência de Lead

## Design Read

Uma central de prospecção com precisão de operador: a cidade é o ponto de
partida, a evidência pública é o material de decisão e o lead enriquecido vira
uma próxima ação concreta.

## Locked Direction

Preservar a direção existente em `DESIGN.md`: energia limpa com precisão de
engenharia. Não introduzir gradientes, cartões decorativos ou uma paleta nova.
Usar o fundo azul-marinho, superfícies claras, amarelo solar para ações e verde
somente para confirmação/status.

## Flow

1. Admin abre `admin/prospeccao-regional`.
2. Informa uma ou mais cidades em um campo por linha.
3. Define opcionalmente o limite, entre 1 e 10 leads por cidade.
4. Confirma a pesquisa e vê estado de execução.
5. O frontend chama o webhook n8n e apresenta a resposta.
6. Admin abre um lead enriquecido e encontra um painel de inteligência antes do
   histórico bruto.

## States

- Empty: instrução curta e exemplo de cidades.
- Editing: cidades inválidas são destacadas sem bloquear as válidas.
- Running: botão bloqueado, contador visual e aviso de que a pesquisa usa dados
  públicos.
- Success: quantidade enriquecida e link para a lista de leads.
- No matches: mensagem neutra, sem tratar ausência de evidência como erro.
- Error: mensagem acionável, mantendo as cidades preenchidas para tentar de novo.
- Mobile: formulário em uma coluna; resultado ocupa a largura inteira.

## Components

### Regional Prospecting Page

- Existing admin shell and navigation.
- Header: `Prospecção regional`, subtitle explaining Brave/public evidence.
- City input: multiline textarea, label explicit, placeholder with Maricá,
  Saquarema, Rio das Ostras and Macaé.
- Limit control: native number input with min 1, max 10, default 10.
- Primary action: `Pesquisar cidades` in solar yellow.
- Privacy note: `Pesquisa somente páginas públicas. Nenhuma mensagem é enviada.`
- Result strip with status, enriched count and request timestamp.

### Lead Intelligence Panel

- Appears only when `Notes` contains the `AI_ENRICHMENT` marker.
- Parse and show city, intent, potential, summary, evidence URL and next action
  as readable fields.
- Keep raw notes editable below the panel.
- Link evidence in a new tab with `rel="noreferrer"` and descriptive text.
- Use a compact evidence layout, not nested cards.

## Accessibility

- Every field has a visible label and an associated error message.
- Status uses text plus color, never color alone.
- Focus remains on the submit control or status region after completion.
- `aria-live="polite"` for running/success/error updates.
- Keyboard order follows city input, limit, submit, result.
- Respect existing reduced-motion behavior.

## API Contract

```text
POST https://n8n.ecosologic.com.br/webhook/regional-lead-enrichment
Header: X-Regional-Lead-Token: configured runtime secret
Body: { cities: string[], maxLeadsPerCity: number }
Response: { status: string, enrichedLeads: number, message: string }
```

The webhook token must not be committed or embedded in the Angular bundle. The
frontend should call a backend proxy or receive the runtime endpoint/token from
an authenticated server configuration. Until that proxy exists, expose only a
backend endpoint for the frontend to call.

## Build Handoff

Implement exactly this spec using the existing Angular standalone components,
`LeadService` conventions and visual tokens. Do not redesign the admin shell.
