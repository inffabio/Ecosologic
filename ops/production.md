# Producao

- Site: `https://www.ecosologic.com.br`
- Servidor: `163.176.79.7`
- Stack: Docker Compose em `/home/hannibal/ecosologic`
- Proxy: Nginx na porta 80/443
- Certificado: Let's Encrypt, renovacao automatica via Snap Certbot

## Operacao

```bash
cd /home/hannibal/ecosologic
docker compose -f docker-compose.server.yml ps
docker compose -f monitoring/docker-compose.monitoring.yml ps
```

O arquivo `.env` de producao contem credenciais e permanece somente no servidor com permissao `600`.
O Docker Compose carrega automaticamente esse `.env` do diretorio do projeto **apenas para interpolar
variaveis** no compose file (por exemplo `${POSTGRES_PASSWORD}`). Essas variaveis nao sao injetadas
automaticamente no ambiente dos containers: entram somente onde o compose file as referencia
explicitamente.

Atencao: `docker compose config` imprime o compose file **ja interpolado, incluindo os segredos**.
Nunca rode `docker compose config` com o `.env` real e compartilhe ou commite a saida. Para validar
sem vazar segredos, use valores dummy (como faz `monitoring/validate.sh`).

## Agendamento (Hangfire) e sincronizacao ANEEL

O agendamento usa Hangfire com armazenamento persistente no PostgreSQL (schema `hangfire`, criado
automaticamente na inicializacao do storage). Nao existe mais `CrmNotificationWorker`/`CrmNotificationLoop`
(BackgroundService) nem a configuracao `Crm:NotificationSyncIntervalSeconds`.

### Jobs recorrentes (IDs estaveis)

| ID | Cron (default) | Timezone | Descricao |
| --- | --- | --- | --- |
| `crm-notifications` | `*/5 * * * *` | - | Sincroniza notificacoes proativas do CRM (deduplicada). |
| `aneel-tariff-sync` | `0 3 1 * *` | `America/Sao_Paulo` | Importa TUSD Fio B da ANEEL (Light e Enel Rio, B1/B2/B3, `R$/MWh`). |

Os IDs sao estaveis e os jobs sao registrados idempotentemente no startup (`AddOrUpdate`), portanto
seguro para multiplas replicas (sem jobs duplicados). Variaveis de ambiente (somente no `.env` do
servidor, nunca versionadas): `CRM_NOTIFICATION_CRON`, `TARIFFS_SYNC_CRON`, `TARIFFS_TIMEZONE_ID`,
`TARIFFS_SOURCE_URL` (obrigatoria), `TARIFFS_HTTP_TIMEOUT_SECONDS`, `TARIFFS_MAX_RETRIES`,
`HANGFIRE_STORAGE_SCHEMA` e `HANGFIRE_DASHBOARD_ENABLED`.

### Dashboard

Rota `/hangfire` na API, restrita ao papel `Admin` (`HangfireAuthorizationFilter`). Habilitada por
`Hangfire:DashboardEnabled` (`HANGFIRE_DASHBOARD_ENABLED`), default `false` em producao.

O Dashboard NAO tem formulario de login proprio. A autorizacao usa o mesmo JWT Bearer da API: o
filtro `HangfireAuthorizationFilter` exige um principal autenticado com papel `Admin`, e essa
identidade vem do middleware `UseAuthentication`, que valida o JWT enviado no header `Authorization`.
Navegacao direta no navegador para `http://127.0.0.1:5157/hangfire` SEM o header
`Authorization: Bearer <token>` retorna `401 Unauthorized` (a rota fica atras do pipeline de
autenticacao/autorizacao e nao redireciona para formulario nem aceita cookie de sessao).

Atencao ao acesso em producao: o nginx publico encaminha somente `/api/` ao container `api`; a rota
`/hangfire` NAO esta exposta publicamente. O container `api` publica a porta `127.0.0.1:5157` no host,
portanto o Dashboard deve ser acessado por tunel SSH. O endpoint de login `POST /api/auth/login` esta
sob `/api/` e e alcancavel publicamente (ou pelo proprio tunel, via `127.0.0.1:5157`).

Procedimento de autenticacao (Admin):

1. Obter o token JWT via `POST /api/auth/login`, enviando o JSON `{"email": "...", "password": "..."}`
   e recebendo `{"token": "<jwt>"}` (o token e assinado com `Auth:JwtKey`, issuer/audience `ecosologic`,
   claim de papel `Admin` e validade de 2 horas):

   ```bash
   curl -s -X POST https://www.ecosologic.com.br/api/auth/login \
     -H 'Content-Type: application/json' \
     -d '{"email":"<ADMIN_EMAIL>","password":"<ADMIN_PASSWORD>"}'
   ```

2. Abrir o tunel SSH e enviar o token no header `Authorization`. Como o navegador nao envia esse
   header em navegacao direta, use um cliente que permita definir o header (exemplo com `curl`):

   ```bash
   ssh -p 46648 -L 5157:127.0.0.1:5157 hannibal@163.176.79.7
   curl -s http://127.0.0.1:5157/hangfire -H "Authorization: Bearer <jwt>"
   ```

   Para usar o Dashboard interativamente no navegador, injete o header `Authorization: Bearer <jwt>`
   na requisicao (por exemplo, uma extensao de modificacao de headers). Nao ha login por formulario
   nem cookie de sessao para o Dashboard. Nao armazene `<jwt>` em arquivos versionados nem em logs.

### Endpoints administrativos (somente papel `Admin`)

- `POST /api/admin/tariffs/sync`: reserva uma execucao (registro `Running`) e enfileira o job no
  Hangfire; retorna `202 Accepted` com o `runId`. Nao executa a importacao de forma sincrona nem
  aceita valores tarifarios no corpo. Este endpoint fica sob `/api/` e e alcancavel pelo nginx publico.
- `GET /api/admin/tariffs/sync/status`: retorna a ultima execucao (status, contadores, `SourceUrl`,
  `SourceHash`, `StartedAt`/`CompletedAt` e erro sanitizado); retorna `404` quando nao ha execucao.

### Retry e lock

- CRM: `AutomaticRetry` com 3 tentativas e atrasos 30s/60s/120s.
- ANEEL: tentativas via `Tariffs:MaxRetries` (default `3`, maximo `10`), backoff exponencial limitado
  (inicio 60s, teto 3600s). Lock distribuido `aneel-tariff-sync` (`DisableConcurrentExecution`,
  timeout de 10 min) compartilhado entre a execucao recorrente e o disparo manual.

### Seguranca de falha ANEEL

A importacao consulta a fonte, normaliza e reconcilia o catalogo tarifario em uma unica transacao
`Serializable` (distribuidoras processadas em conjunto). A execucao so e marcada `Succeeded` apos o
commit; qualquer falha desfaz tarifas e auditoria juntas, marca a execucao como `Failed` (erro
sanitizado, sem token/payload) e reapresenta a excecao para retry do Hangfire. A ultima versao
tarifaria valida nunca e removida nem invalidada; nao ha fallback para valores inventados.

### Validacao e recuperacao (PostgreSQL) — NAO EXECUTADA

> Status: **NAO EXECUTADO neste ambiente.** Os comandos abaixo sao o procedimento exato a rodar no
> servidor de producao, e os "resultados esperados" descrevem o que uma execucao bem-sucedida deveria
> retornar. **Nenhum comando foi executado aqui e nenhum resultado foi verificado**: o schema, as
> tabelas e os jobs recorrentes do Hangfire **nao foram observados** durante a redacao deste documento.
> Nao assuma que o schema `hangfire` ou a tabela `aneel_tariff_imports` ja existam em producao.

As migrations do EF Core sao aplicadas automaticamente no startup pelo `DatabaseMigrator` (retry com
backoff via `Database:MigrateMaxAttempts`/`Database:MigrateBackoffSeconds`). O schema do Hangfire e as
tabelas do Hangfire sao criados pelo storage na inicializacao, quando o container `api` sobe.

#### Comandos exatos (a executar no servidor de producao)

Para validar sem expor segredos (a senha vem do ambiente do proprio container, como no script de
backup; nunca passe credenciais por argumento nem rode `docker compose config` com o `.env` real):

```bash
cd /home/hannibal/ecosologic
docker compose -f docker-compose.server.yml exec -T postgres sh -c \
  'PGPASSWORD="${POSTGRES_PASSWORD:-}" psql -w -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DB:-ecosologic}"' <<'SQL'
SELECT nspname AS schema_hangfire FROM pg_namespace WHERE nspname = 'hangfire';
SELECT tablename FROM pg_tables WHERE schemaname = 'hangfire' ORDER BY tablename;
SELECT key, value FROM "hangfire"."set" WHERE key = 'recurring-jobs';
SELECT "Status", "StartedAt", "CompletedAt", "InsertedProfileCount", "ClosedProfileCount", "UnchangedProfileCount", "ErrorMessage"
FROM "aneel_tariff_imports" ORDER BY "StartedAt" DESC LIMIT 5;
SQL
```

#### Resultados esperados (nao verificados)

- O schema `hangfire` existe (com as tabelas `set`, `job`, `state`, `lock`, `server`, etc.).
- O registro `recurring-jobs` em `"hangfire"."set"` contem os IDs `crm-notifications` e
  `aneel-tariff-sync`.
- A tabela `aneel_tariff_imports` existe (vazia ou com o historico de execucoes).

#### Resultados verificados

Nenhum. Nada foi executado neste ambiente; nao ha saida real registrada.

#### Schema configuravel

Os exemplos acima usam o schema **default** `hangfire` (`Hangfire:StorageSchema`, definido pela
variavel `HANGFIRE_STORAGE_SCHEMA`, default `hangfire`). Se `HANGFIRE_STORAGE_SCHEMA` for definido com
outro valor, substitua `hangfire` pelo valor configurado **editando o script**, mantendo o identificador
entre aspas duplas — por exemplo `SELECT key, value FROM "meu_schema"."set" ...` e
`WHERE schemaname = 'meu_schema'`. Nao interpole a variavel de ambiente dentro do SQL (algo como
`FROM "$HANGFIRE_STORAGE_SCHEMA"."set"` nao produz um identificador valido e e inseguro); o nome do
schema deve aparecer como identificador literal no texto. A tabela de dominio `aneel_tariff_imports`
NAO pertence ao schema do Hangfire: fica no schema padrao da aplicacao (`public`), independente de
`HANGFIRE_STORAGE_SCHEMA`.

Recuperacao: um registro `Running` sem job ativo e recuperavel disparando nova sincronizacao via
`POST /api/admin/tariffs/sync` (e confirmando em `GET .../sync/status`); o lock distribuido expira
automaticamente apos 10 min. Para reaplicar migrations e religar os jobs recorrentes, basta recriar o
container `api` (`docker compose -f docker-compose.server.yml up -d api`).

## Chaves de protecao de dados

O servico `api` persiste as chaves do ASP.NET Data Protection em um volume nomeado `api-data-protection-keys` (nome Docker fixo `ecosologic_api_data_protection_keys`), montado em `/root/.aspnet/DataProtection-Keys`. A persistencia e configurada explicitamente no `Program.cs` via `AddDataProtection().PersistKeysToFileSystem(...)`.

Motivo: o Data Protection sera usado para proteger cookies, tokens antifalsificacao e outros dados protegidos no futuro. Sem persistencia, as chaves seriam regeneradas a cada restart do container, invalidando sessoes/cookies ativos e causando falhas intermitentes de autenticacao. Um volume nomeado mantem as chaves entre recriacoes do container.

A autenticacao atual continua usando JWT com a chave separada `Auth__JwtKey` (variavel de ambiente `AUTH_JWT_KEY`) e nao depende do Data Protection.

O volume e nomeado (nao um bind mount dentro da arvore do projeto), mas as chaves permanecem armazenadas e acessiveis ao Docker/root no filesystem do host, sob o diretorio de dados do Docker. O container roda como `root`, portanto a montagem em `/root/.aspnet/...` tem ownership compativel sem configuracao extra de permissoes.

## Upload de midia

O endpoint `POST /api/content/media` (restrito ao papel `Admin`) aceita apenas imagens raster:

- Formatos permitidos: JPEG, PNG e WebP.
- Tamanho maximo: 5 MB (`MediaConstraints.MaxBytes`).
- Imagens animadas sao rejeitadas: o endpoint aceita apenas imagens de frame unico
  (`MediaConstraints.MaxFrames = 1`). O numero de frames e detectado via
  `Image.Identify` (metadados do decoder) antes do decode completo, impedindo
  decompression-bombs por meio de APNG/WebP/GIF animados com muitos frames.
- A validacao usa a assinatura real (magic bytes) do conteudo, nao o `ContentType`/extensao
  declarados pelo cliente. Arquivos cuja assinatura nao corresponde ao tipo/extensao declarados
  e arquivos truncados/corrompidos sao rejeitados com `400`, nunca `500`.
- Antes de gravar, a imagem e reencodada (via `SixLabors.ImageSharp`, versao pinada `3.1.12`)
  para remover metadados e payloads incorporados (EXIF/IPTC/XMP/ICC). O storage recebe somente
  o conteudo sanitizado; o `IFormFile` original nunca e persistido.
- A decodificacao/reencodificacao (`Image.Identify` + `Image.Load` + encode) e limitada a no
  maximo `2` operacoes simultaneas por processo, via `SemaphoreSlim` em `ImageSanitizer`
  (constante `MaxConcurrentImageDecodes`). O limite evita picos de memoria quando varios uploads
  chegam ao mesmo tempo. Requests acima do limite aguardam de forma cancelavel (`WaitAsync` com o
  `CancellationToken` do request); o slot e liberado em `finally`, inclusive em falha. O endpoint
  nao retorna `429`/`503` nesse caso: ele apenas aguarda dentro do limite.
- O nome do arquivo e sempre gerado pelo servidor (`Guid`), nunca derivado do nome enviado.
- JPEG e WebP sao reencodados com qualidade `90`; PNG e reencodado sem perdas.

Arquivos sao gravados em `Storage:MediaPath` (default `wwwroot/uploads`) e servidos
estaticamente em `/uploads/`. O formato WebP depende do suporte do ambiente de execucao; se
nao estiver disponivel, o upload WebP retorna `400` em vez de gravar o conteudo cru.

## Atencao: nao remova o volume de chaves

Os comandos abaixo apagam as chaves de protecao de dados e invalidam imediatamente sessoes e cookies ativos, alem de quaisquer tokens antifalsificacao assinados com as chaves antigas:

- `docker compose down -v`
- `docker volume prune`
- `docker volume rm ecosologic_api_data_protection_keys`

Consequencia: os usuarios precisarao se autenticar novamente e cookies/tokens emitidos antes da remocao deixam de ser validos. Evite esses comandos fora de janelas de manutencao planejadas.

O backup deste volume (junto com `media-data`) e tratado pelo script de backup descrito na secao "Backup" abaixo. Ele nunca usa `down -v` e nao para a producao.

## Backup

O backup e automatizado por script e timer systemd. Os artefatos estao prontos em `ops/`,
mas **ainda nao foram instalados no servidor** (este passo apenas prepara os arquivos;
nenhum deploy remoto e feito aqui).

### O que e backupeado

- `postgres` (banco da aplicacao): dump custom/comprimido via `pg_dump -Fc`. O `pg_dump`
  roda DENTRO do container e resolve a `PGPASSWORD` a partir do proprio ambiente do
  container, portanto a senha nunca aparece em argumentos de linha de comando nem em logs.
  O script **nao le** o `.env`.
- Volume `media-data` (nome Docker `ecosologic_media-data`): arquivo `tar.gz` do conteudo.
- Volume `api-data-protection-keys` (nome Docker `ecosologic_api_data_protection_keys`):
  arquivo `tar.gz` das chaves de protecao de dados.

### Layout dos backups

Cada execucao grava em um staging temporario e, somente apos validar todos os artefatos,
renomeia atomicamente o diretorio para um conjunto completo e timestamped:

```
/home/hannibal/backups/ecosologic/
  .backup.lock/            # lock de exclusao mutua
  20260102-033000/         # conjunto completo (dir com os 3 artefatos = completo)
    db.dump
    media.tar.gz
    keys.tar.gz
```

### Arquivos

| Arquivo | Finalidade |
| --- | --- |
| `ops/backup-ecosologic.sh` | Script POSIX de backup (configuravel por variaveis de ambiente). |
| `ops/restore-check-ecosologic.sh` | Valida um conjunto de backups em diretorio temporario, sem tocar producao. |
| `ops/systemd/ecosologic-backup.service` | Unidade systemd (roda como `hannibal`, invoca o script via `/bin/sh`). |
| `ops/systemd/ecosologic-backup.timer` | Timer diario as 03:30. |
| `ops/systemd/ecosologic-backup-failure.service` | Alerta de falha via journal (acionado por `OnFailure`). |
| `ops/validate-backup.sh` | Checagens estaticas (sintaxe `sh -n`, segredos, `down -v`, permissoes, paths, tar seguro, unidades systemd). |

### Como funciona (seguranca)

- Diretorio de backup criado com `chmod 700`; artefatos com `chmod 600` (`umask 077`).
- Preflight: antes de qualquer acao, cada script verifica todas as ferramentas e
  opcoes de que depende (`docker`, `docker compose version` (plugin Compose v2),
  `realpath`, `date`, `tar`, `find`, `mktemp`, `id`, `stat -c`,
  `cp --reflink=never`, `date -d`, `sha256sum`, `awk`, `sed`, `grep`, `sort`,
  etc.) e falha cedo e claramente se algo faltar, sem tocar em nenhum estado
  (nem no root de backup, nem no lock, nem em containers).
- O tar dos volumes roda em container descartavel com `--network none`; o tar grava na
  saida padrao (`tar -czf -`) e o host redireciona para o arquivo, de modo que o arquivo
  final e sempre criado pelo usuario que invoca (nunca root-owned). O volume de chaves e
  root-owned e, por isso, e lido por um tar rodando como root (`--user 0`); o arquivo
  continua sendo criado pelo usuario do host e recebe `chmod 600` como defesa em
  profundidade.
- Lock por diretorio impede execucoes concorrentes; lock obsoleto e detectado e removido.
- Validacao e canonizacao de caminhos antes de qualquer acao destrutiva: `BACKUP_ROOT` deve
  ser absoluto, sem componentes `.`/`..`, e ser canonizado com `realpath` (resolvendo
  symlinks) para um caminho estritamente sob `/home/hannibal`. `LOCK_FILE` e derivado do
  root canonico. Symlinks e caminhos nao-canonicos sao recusados. A limpeza (inclusive em
  traps) so remove filhos diretos de `BACKUP_ROOT` (`safe_rm_rf`), revalida o alvo com
  `realpath` e nunca segue symlinks nem remove `rm -rf` em caminho arbitrario.
- Artefatos sao gravados em staging e o diretorio e renomeado atomicamente para o destino
  somente apos a validacao (`pg_restore --list` para o dump e `tar -tf` para os volumes).
  Qualquer falha faz o script sair com exit code nao-zero antes de publicar o conjunto.
- `RETENTION_DAYS` e validado como inteiro entre `1` e `365`; se o `find` da retencao
  falhar, o script aborta (o status do `find` e checado explicitamente, sem mascara por
  pipe).
- Retencao remove somente diretorios completos e antigos (contendo os 3 artefatos), nunca
  artefatos individuais nem arquivos alheios.
- Nenhum `.env` ou segredo e incluido nos arquivos de backup.

### Configuracao (variaveis de ambiente / defaults)

| Variavel | Default | Descricao |
| --- | --- | --- |
| `PROJECT_DIR` | `/home/hannibal/ecosologic` | Diretorio da stack. |
| `COMPOSE_FILE` | `docker-compose.server.yml` | Compose file usado para o `exec`. |
| `BACKUP_ROOT` | `/home/hannibal/backups/ecosologic` | Destino dos backups (deve ser absoluto, sem `.`/`..`, canonizado com `realpath` e resolvendo sob `/home/hannibal`). |
| `RETENTION_DAYS` | `14` | Dias de retencao (inteiro entre `1` e `365`). |
| `POSTGRES_SERVICE` | `postgres` | Nome do servico Postgres no compose. |
| `MEDIA_VOLUME` | `ecosologic_media-data` | Nome do volume de midia. |
| `KEYS_VOLUME` | `ecosologic_api_data_protection_keys` | Nome do volume de chaves. |

Para sobrescrever, adicione `Environment=` na unidade systemd ou exporte a variavel antes
de rodar o script manualmente.

### Restore check (sem tocar producao)

```bash
cd /home/hannibal/ecosologic
sh ops/restore-check-ecosologic.sh [BACKUP_DIR] [STAMP]
```

- PostgreSQL: executa `pg_restore --list` e, por padrao (`VERIFY_FULL_RESTORE=1`), sobe um
  container PostgreSQL isolado e descartavel (senha dummy) e faz uma restauracao real
  seguida de uma query de contagem de tabelas. A restauracao real usa
  `pg_restore --no-owner --no-privileges`: o dump de producao registra a role de producao
  (ex.: `ecosologic_app`) e seus `GRANT`s, mas o container descartavel so tem o superusuario
  `postgres` com senha dummy, entao restaurar owners/privilegios falharia com "role ... does
  not exist". Pular essas instrucoes ainda restaura o schema e os dados completos, que e
  tudo o que o restore check precisa para provar que o dump e restauravel (a alternativa de
  recriar a role de producao no container exporia o nome/layout de grants de producao em um
  ambiente efemero sem beneficio). O dump **nunca e bind-montado**: e transmitido por stdin
  (`docker run -i` / `docker exec -i`), entao a copia privada do `db.dump` nao precisa de
  permissoes amplas (o arquivo continua `0600`, dono do usuario invocador). O container
  recebe nome aleatorio (nao previsivel) e uma label de ownership;
  o cleanup remove somente o container criado nesta execucao, nunca um container
  preexistente, e a remocao e retentada e confirmada como ausente via `docker inspect`
  (o nome so e limpo depois que o `docker inspect` confirma que o container nao existe
  mais, distinguindo `No such object` de um erro de daemon/comando, que e tratado como
  estado desconhecido e nunca como "container ausente"). Se a remocao falhar ou o estado
  ficar desconhecido, o container e preservado com seu nome e label para diagnostico e o
  script sai com exit code nao-zero (nunca sucesso silencioso).
- `BACKUP_DIR` e restrito ao root de backup permitido (`BACKUP_ALLOWED_ROOT`, default
  `/home/hannibal/backups`). Ambos devem ser absolutos, existentes, sem componentes `.`/`..`,
  nao-symlinks e canonizados com `realpath` (resolvendo symlinks); `BACKUP_ALLOWED_ROOT`
  precisa resolver estritamente sob `/home/hannibal` e `BACKUP_DIR` dentro dele. O script
  **nao le producao**.
- Sem `STAMP`, o script considera **somente diretorios cujo nome e um timestamp estrito**
  `YYYYMMDD-HHMMSS` que tambem seja **semanticamente valido** (o regex sozinho aceitaria
  datas impossiveis como mes 99 ou hora 25; o script valida com `date -d` e ignora
  diretorios com data impossivel), itera do mais recente para o mais antigo e escolhe o
  primeiro conjunto **completo** (`db.dump`, `media.tar.gz` e `keys.tar.gz` todos arquivos
  regulares, nao-symlinks e com link count 1 / sem hardlinks); diretorios estrangeiros,
  timestamps impossiveis e conjuntos parciais sao ignorados. Se nao houver nenhum conjunto
  completo, falha claramente.
- Ao iniciar, os artefatos de **origem** do conjunto selecionado sao validados (arquivo
  regular, nao-symlink, link count 1 via `stat -c %h`, sem hardlinks) **antes** de serem
  copiados para um `WORKDIR` privado (`chmod 700`) com `cp --reflink=never -P` (copia real e
  independente, sem seguir symlinks). As copias privadas sao entao validadas novamente
  (nao-symlink, arquivo regular, sem hardlink, caminho canonico dentro do conjunto) e apenas
  elas sao lidas pelo `pg_restore`/`tar`. A copia em si e verificada: a identidade da fonte
  (device/inode/tamanho/mtime/birth via `stat -c %d:%i:%s:%Y:%W`) e comparada antes e depois
  da copia e novamente apos o checksum, e a copia e comparada a fonte com `sha256sum`
  (digest criptografico, em vez do fraco `cksum`; se `sha256sum` nao estiver disponivel, o
  script falha claramente). Nao ha alegacao de atomicidade (somente um snapshot de filesystem
  seria verdadeiramente atomico): se a fonte mudar durante a copia ou o checksum, o script
  falha em vez de operar sobre um snapshot truncado, e nada apos a copia depende da fonte.
- Volumes: valida integridade com `tar -tf`. A validacao das entradas do `tar.gz` (path
  traversal, caminhos absolutos, symlinks e hardlinks) e **sempre obrigatoria**; com
  `VERIFY_EXTRACT=1` tambem e feita a extracao completa. Validacao e extracao operam sobre
  a copia privada do arquivo.
- Todos os containers temporarios rodam com `--network none` (sem rede). A restauracao usa
  `docker exec` sobre o socket unix local do container isolado, sem depender de rede.
- Nenhum volume de producao e montado; nada le ou altera dados de producao.

### Alerta e verificacao

O servico `ecosologic-backup.service` tem `OnFailure=ecosologic-backup-failure.service`, que
registra no journal uma mensagem identificavel (tag `ecosologic-backup-failure`, marcador
`ECOSOLOGIC_BACKUP_FAILED`). Para verificar a saude do backup:

```bash
systemctl status ecosologic-backup.service ecosologic-backup.timer
systemctl list-timers ecosologic-backup.timer                 # ultima e proxima execucao
journalctl -u ecosologic-backup.service -n 50                 # logs da ultima execucao
journalctl -t ecosologic-backup-failure                       # somente alertas de falha
journalctl | grep ECOSOLOGIC_BACKUP_FAILED                    # busca por falhas
```

`systemctl list-timers` mostra a coluna `LAST` (ultima execucao) e `NEXT`; o resultado da
ultima execucao fica em `systemctl show ecosologic-backup.service -p Result -p ExecMainStatus`.

### Instalacao (ainda nao aplicada)

Pre-requisito: o usuario `hannibal` deve estar no grupo `docker` (acesso ao daemon sem sudo).

```bash
sudo install -m 0644 \
  ops/systemd/ecosologic-backup.service \
  ops/systemd/ecosologic-backup-failure.service \
  ops/systemd/ecosologic-backup.timer \
  /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now ecosologic-backup.timer
```

A instalacao usa `sudo` uma unica vez (etapa administrativa). Apos habilitado, o timer
roda diariamente as 03:30 e o servico executa como `hannibal` sem exigir senha sudo em
cada execucao. O servico invoca o script via `/bin/sh` (`ExecStart=/bin/sh
/home/hannibal/ecosologic/ops/backup-ecosologic.sh`), portanto nao depende do bit
executavel do script (`chmod +x` nao e necessario).
