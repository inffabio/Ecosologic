# Supplier Discovery Deduplication Design

## Goal

Executar novamente a descoberta de fornecedores sem criar duplicatas, preservando fornecedores já existentes no CRM e aceitando URLs longas como fonte.

## Decisions

- A identidade de um fornecedor será o nome normalizado com `Trim()` e comparação sem distinção de maiúsculas/minúsculas.
- Duplicatas no mesmo lote serão ignoradas, mantendo a primeira ocorrência.
- Fornecedores já existentes não serão sobrescritos nem terão status alterado.
- Novos fornecedores continuarão entrando como `Pending`.
- A coluna `Source` terá limite de 512 caracteres, suficiente para URLs de pesquisa.
- O workflow continuará usando uma página de até 10 resultados do Tavily, com Brave como fallback; a deduplicação final ficará protegida também pela API.

## Data Flow

1. Tavily ou Brave retorna páginas de busca.
2. 9Router extrai candidatos com nome, website, contato e fonte.
3. O workflow envia os candidatos ao endpoint de importação.
4. A API normaliza nomes, remove duplicatas do lote e consulta nomes existentes.
5. Somente novos fornecedores são persistidos.
6. A resposta informa os registros efetivamente inseridos.

## Error Handling

- Lista vazia retorna sucesso sem inserir registros.
- Nome vazio é descartado.
- Colisões por caixa, espaços laterais ou repetição no mesmo lote são tratadas como o mesmo fornecedor.
- A restrição única do banco permanece como última barreira contra concorrência.

## Implementation Note

The production database already contained the supplier table from an earlier deployment attempt. The schema migration therefore alters the existing `Source` column instead of recreating the table.

## Testing

- Teste de unidade para deduplicação case-insensitive no mesmo lote.
- Teste de integração/API para preservar registros existentes.
- Teste de persistência para fonte com mais de 64 caracteres.
- Execução real do workflow com contagem antes/depois e verificação de nomes únicos.
