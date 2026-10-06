# Runbook: Descoberta de Fornecedores Solares

## Objetivo

O workflow **Ecosologic | Descoberta de Fornecedores Solares** pesquisa
fornecedores de sistemas solares, normaliza os resultados com o 9Router e
importa candidatos no CRM para revisão administrativa.

Workflow ID no n8n:

```text
bsBYAZDTEfqBx5j3
```

O workflow permanece inativo até uma execução manual ou ativação controlada.

## Fluxo Atual

```text
Manual Trigger
  -> recuperar TAVILY_API_KEY pelo broker OCI
  -> Tavily Search (primário)
  -> Brave Search (fallback de cota/indisponibilidade)
  -> recuperar 9ROUTER_API_KEY pelo broker OCI
  -> consultar modelos do 9Router
  -> normalizar fornecedores em JSON
  -> autenticar no CRM
  -> POST /api/solar-suppliers/import
```

## Limites e Duplicatas

Cada execução consulta até 10 resultados do Tavily. O Brave usa até 10
resultados quando acionado como fallback. O 9Router pode extrair mais de um
fornecedor por página, como ocorreu na primeira execução, que retornou 15
fornecedores.

A importação é idempotente por nome, sem distinção de maiúsculas/minúsculas e
ignorando espaços laterais. Duplicatas no mesmo lote e fornecedores já
existentes no CRM são descartados; os registros existentes não são sobrescritos
nem têm seu status alterado. Uma nova execução pode, portanto, ser feita para
descobrir fornecedores adicionais sem duplicar os atuais.

O campo de fonte aceita URLs de até 512 caracteres. Todos os candidatos entram
como `Pending` e devem ser revisados antes da aprovação administrativa.

A execução atual usa o endpoint REST da Tavily. A credencial **Tavily MCP**
também está criada no n8n para a futura conversão do nó de busca para
**MCP Client Tool**.

## Segredos OCI

O n8n não monta `.oci` e não acessa o Vault diretamente. A API .NET faz isso
por meio do endpoint protegido:

```text
GET /api/internal/secrets/{name}
X-Internal-Secrets-Token: <token interno>
```

Mapeamento lógico para nomes do OCI Vault:

```text
N8N_API_KEY      -> N8N-APIKEY
TAVILY_API_KEY   -> TAVILY-API-KEY
TAVILY_MCP       -> TAVILY-MCP
9ROUTER_API_KEY  -> 9ROUTER-API-KEY
BRAVE_API_KEY    -> BRAVE-API-KEY
```

O endpoint mantém cache em memória por 300 segundos por padrão e nunca deve
registrar valores de segredos nos logs.

## Configuração do Servidor

A API .NET precisa destas variáveis no ambiente de produção:

```env
OCI_CONFIG_FILE=/app/.oci/config
OCI_PROFILE=DEFAULT
OCI_VAULT_OCID=ocid1.vault.oc1...
OCI_CONFIG_HOST_PATH=/home/hannibal/.oci
INTERNAL_SECRETS_TOKEN=<token aleatório>
INTERNAL_SECRETS_CACHE_SECONDS=300
OCI_SECRET_N8N_API_NAME=N8N-APIKEY
OCI_SECRET_TAVILY_API_NAME=TAVILY-API-KEY
OCI_SECRET_TAVILY_MCP_NAME=TAVILY-MCP
OCI_SECRET_9ROUTER_API_NAME=9ROUTER-API-KEY
```

O container da API monta somente:

```text
/home/hannibal/.oci:/app/.oci:ro
```

O Cryptographic Endpoint da OCI não é necessário para leitura de segredos.

## Credenciais n8n

### 9Router

Para nós OpenAI-compatible:

```text
Base URL: https://9router.ecosologic.com.br/v1
API Key: valor de 9ROUTER-API-KEY
```

### Tavily REST

O valor de `TAVILY-API-KEY` deve ser a chave bruta Tavily, iniciando com
`tvly-`. Não deve ser uma URL MCP.

### Tavily MCP

A credencial **Tavily MCP** usa o valor de `TAVILY-MCP`. O endpoint remoto é:

```text
https://mcp.tavily.com/mcp/?tavilyApiKey=<chave>
```

Nunca grave a URL completa com a chave no repositório, em documentação ou no
JSON exportado do workflow.

### Brave Search

O segredo `BRAVE-API-KEY` deve conter uma chave ativa da Brave Search API. O
workflow usa o Brave como fallback quando a Tavily retornar erro, atingir cota
ou ficar indisponível.

## Revisão no CRM

Os resultados são importados como candidatos descobertos. A revisão ocorre
em:

```text
/admin/fornecedores
```

Os candidatos devem ser conferidos antes de aprovação, especialmente nome,
site, contato e fonte. Telefone, WhatsApp e contato responsável podem ser
completados manualmente antes da aprovação.

## Troubleshooting

### Broker retorna `401`

Verifique o header `X-Internal-Secrets-Token` e confirme que o token usado no
n8n é igual ao `INTERNAL_SECRETS_TOKEN` da API.

### Broker retorna `404`

O nome lógico não está na allowlist ou o nome físico configurado não existe
no Vault.

### Tavily retorna `401`

Confirme que `TAVILY-API-KEY` contém uma chave Tavily ativa e não a URL MCP.

### 9Router retorna erro de modelo

Consulte `/v1/models` no 9Router e use um modelo atualmente disponível. A
lista pode mudar conforme os provedores conectados.

### CRM retorna `401`

O workflow precisa obter um JWT administrativo por `/api/auth/login` antes de
chamar `/api/solar-suppliers/import`.

### CRM retorna `500` ao importar uma fonte longa

Confirme que a migration `ExpandSolarSupplierSource` foi aplicada e que
`solar_suppliers.Source` está como `varchar(512)`.

### Não publicar segredos

Não incluir em commits, exports de workflow, logs ou mensagens:

- valores de chaves OCI
- chave privada `.pem`
- token interno
- API keys Tavily, 9Router ou n8n
