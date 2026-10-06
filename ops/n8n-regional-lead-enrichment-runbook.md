# Runbook: Enriquecimento Regional de Leads

## Objetivo

Pesquisar sinais públicos de interesse em energia solar por cidade e enriquecer
leads novos do CRM com resumo comercial, evidência, próxima ação e tarefa de
follow-up.

Workflow n8n:

```text
8eyr8ZAENERzZpDE
```

Webhook:

```text
POST https://n8n.ecosologic.com.br/webhook/regional-lead-enrichment
```

O workflow usa Brave Search para páginas públicas e 9Router para análise. Ele
permanece limitado a 20 cidades por execução e até 10 resultados por cidade.

## Payload

```json
{
  "cities": ["Marica", "Saquarema", "Rio das Ostras", "Macae"],
  "maxLeadsPerCity": 10
}
```

Enviar o token da credencial do webhook no header:

```text
X-Regional-Lead-Token: <token armazenado no cofre operacional>
```

O limite aceito para `maxLeadsPerCity` é de 1 a 10. Cidades repetidas são
normalizadas e processadas uma única vez.

## Resultado

Resposta de sucesso:

```json
{
  "status": "completed",
  "enrichedLeads": 1,
  "message": "1 lead(s) enriquecido(s) com pesquisa pública do Brave."
}
```

Para cada lead elegível, o workflow:

- adiciona um bloco `AI_ENRICHMENT` em `Notes`;
- registra uma atividade do tipo `AIEnrichment`;
- cria uma tarefa de follow-up para o dia seguinte;
- preserva as notas existentes;
- não processa novamente leads que já possuem o bloco de enriquecimento.

## Critérios de segurança

- Brave pesquisa somente conteúdo público indexado.
- Não acessar perfis privados, mensagens diretas ou páginas que exigem login.
- Não enviar mensagens automaticamente para os leads.
- A evidência precisa estar associada à cidade pesquisada.
- Não incluir tokens, senhas ou chaves no payload, workflow exportado ou logs.

## Troubleshooting

### `401`

Confirme o header `X-Regional-Lead-Token` e use a credencial operacional correta.

### `400`

O payload não contém uma lista `cities` válida.

### `500` no CRM

Verifique os logs da API e confirme que o workflow está usando as credenciais
criptografadas de CRM, Brave e 9Router. Atividades são serializadas sem a
referência circular ao lead.

### Nenhum lead enriquecido

Os leads podem já conter `AI_ENRICHMENT`, não estar na etapa `New` ou não haver
evidência pública suficiente para a cidade pesquisada.
