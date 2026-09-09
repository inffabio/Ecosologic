# Hangfire e Sincronizacao ANEEL

## Objetivo

Substituir o processamento agendado atual baseado em `BackgroundService` por Hangfire e adicionar uma rotina persistente para importar mensalmente os valores de TUSD/Fio B da ANEEL para Light e Enel Distribuicao Rio.

## Escopo

- Migrar as notificacoes proativas do CRM para um job recorrente Hangfire a cada 5 minutos.
- Remover o worker, loop e configuracao de intervalo antigos depois da migracao.
- Configurar Hangfire com PostgreSQL, usando a infraestrutura de banco existente.
- Proteger o Hangfire Dashboard para usuarios com papel `Admin`.
- Criar um job mensal de sincronizacao tarifaria da ANEEL.
- Filtrar os dados por:
  - `Light Servicos de Eletricidade S.A.`
  - `Enel Distribuicao Rio`
  - subgrupos `B1`, `B2` e `B3`
  - componente `TUSD Fio B`
  - unidade `R$/MWh`
- Importar tarifas de forma idempotente e versionada.
- Preservar versoes antigas para reproducibilidade de calculos e propostas.
- Disponibilizar disparo manual e consulta do ultimo resultado para administradores.

Ficam fora deste escopo os subgrupos `B4`, os calculos especificos de demanda do grupo A e qualquer alteracao na formula de calculo do Fio B.

## Arquitetura

O Hangfire sera registrado no projeto API com armazenamento persistente no PostgreSQL. Jobs recorrentes serao registrados com IDs estaveis durante a inicializacao da aplicacao. O job de notificacoes continuara chamando o servico de aplicacao existente, criando um escopo de DI por execucao. O job ANEEL sera separado em cliente/fonte externa, normalizador e servico de reconciliacao do catalogo, sem colocar HTTP ou parsing dentro de `TariffCatalog`.

O armazenamento do Hangfire devera usar schema/tabelas isolados ou prefixo configurado, sem misturar suas tabelas com as tabelas de dominio. A inicializacao devera ser segura para multiplas replicas e nao podera criar jobs duplicados.

## Agendamento

- CRM: job recorrente com expressao equivalente a cada 5 minutos, mantendo o comportamento de execucao frequente atual.
- ANEEL: job recorrente no primeiro dia de cada mes, em horario e timezone configuraveis.
- Os IDs recorrentes serao constantes, por exemplo `crm-notifications` e `aneel-tariff-sync`.
- O Hangfire controlara retries e concorrencia; o job ANEEL tambem devera impedir sobreposicao logica por escopo de distribuidora/periodo.
- O sistema devera registrar logs estruturados com job ID, inicio, fim, quantidade lida, quantidade importada e motivo de falha.

## Sincronizacao ANEEL

O job consultara a base tarifaria oficial do portal ANEEL e aplicara os filtros definidos neste documento. Como o portal e um relatorio Power BI incorporado, a implementacao devera encapsular o mecanismo de consulta em um adaptador da ANEEL, permitindo testar parsing e normalizacao sem depender da rede.

Cada registro normalizado devera conter, no minimo:

- distribuidora;
- grupo e subgrupo;
- modalidade e posto, quando aplicaveis;
- componente tarifario;
- valor numerico e unidade original;
- inicio e fim da vigencia;
- URL da fonte;
- identificador de resolucao/documento, quando fornecido;
- data de consulta;
- hash do documento ou payload consultado.

Valores em `R$/MWh` deverao ser convertidos somente no limite exigido pelos consumidores atuais, preservando tambem a unidade original e a evidencia da fonte. O valor usado pelo calculo devera permanecer semanticamente claro para evitar confundir TUSD Fio B com tarifa total de energia.

Uma sincronizacao repetida com os mesmos dados nao devera criar nova versao. Quando houver alteracao real, a versao anterior permanecera fechada na sua vigencia e uma nova versao sera inserida em transacao atomica, sem intervalos sobrepostos. Registros incompletos, ambiguos ou sem fonte valida nao serao publicados como tarifa elegivel.

Se a ANEEL estiver indisponivel, mudar o formato ou retornar dados incompletos, o job devera falhar de forma observavel, manter a ultima versao valida e registrar o erro. Nao havera fallback para valores inventados ou fixtures de teste.

## Persistencia e auditoria

Adicionar uma entidade de execucao/importacao para registrar:

- identificador da execucao;
- origem e filtros usados;
- inicio, fim e status (`Running`, `Succeeded`, `Failed`);
- contagens de registros lidos, aceitos, ignorados e publicados;
- hash da fonte;
- mensagem de erro sanitizada;
- identificador do job Hangfire.

O catalogo tarifario continuara sendo a fonte usada pelos calculadores. A entidade de execucao servira para auditoria e diagnostico, nao para substituir os snapshots tarifarios existentes.

## API administrativa

- `POST /api/admin/tariffs/sync`: enfileira uma sincronizacao e retorna o identificador da execucao.
- `GET /api/admin/tariffs/sync/status`: retorna a ultima execucao e seu status.
- Ambos exigem autenticacao e papel `Admin`.
- A requisicao manual nao executara a importacao de forma sincrona nem aceitara valores tarifarios no corpo.
- O catalogo publico/consumidor continuara somente leitura.

## Configuracao

Adicionar configuracoes para:

- conexao/storage do Hangfire;
- timezone do agendamento ANEEL;
- cron do CRM;
- cron mensal ANEEL;
- URL da fonte ANEEL;
- timeout e limites de retry do cliente ANEEL;
- habilitacao do Dashboard.

Segredos deverao continuar vindo de variaveis de ambiente ou secret store, nunca de arquivos versionados.

## Falhas e seguranca

- Dashboard Hangfire somente para `Admin`.
- Endpoints manuais somente para `Admin`.
- Erros externos nao devem incluir tokens, credenciais ou payloads sensiveis nos logs.
- Falhas de uma distribuidora nao devem publicar parcialmente a outra dentro da mesma execucao; a politica sera transacao por lote e rollback do lote com erro.
- O job de notificacoes devera manter deduplicacao, escopo de servico e cancelamento controlado.
- A aplicacao devera evitar duas instancias de servidor Hangfire processando o mesmo trabalho fora das garantias do storage.

## Testes e aceite

- Testar o registro dos jobs recorrentes com IDs estaveis.
- Testar a migracao do comportamento de notificacoes: execucao, repeticao, falha recuperavel, deduplicacao e cancelamento.
- Testar autorizacao do Dashboard e dos endpoints administrativos.
- Testar parsing de fixtures ANEEL, filtros Light/Enel e B1/B2/B3, componente Fio B e unidade `R$/MWh`.
- Testar conversao de unidade e preservacao da origem.
- Testar idempotencia, mudanca de hash, fechamento de vigencia, ausencia de sobreposicao e rollback.
- Testar indisponibilidade e resposta incompleta da ANEEL sem perda da ultima tarifa valida.
- Validar build, suite completa e migration em ambiente PostgreSQL.

## Criterios de aceite

1. Nao existe mais `CrmNotificationWorker` registrado ou executando.
2. O Hangfire processa notificacoes CRM a cada 5 minutos com o comportamento anterior preservado.
3. O job mensal ANEEL e persistente, observavel e executavel manualmente por Admin.
4. Somente registros Light/Enel RJ, B1/B2/B3 e TUSD Fio B entram no catalogo elegivel.
5. Reprocessar a mesma fonte nao duplica tarifas.
6. Uma falha de consulta nao remove nem invalida a ultima tarifa publicada.
7. Todas as tarifas publicadas possuem vigencia e evidencia da fonte.
