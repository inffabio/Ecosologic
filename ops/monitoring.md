# Observabilidade

A stack de monitoramento usa Prometheus, Grafana, Alertmanager, Node Exporter, cAdvisor, PostgreSQL Exporter e Blackbox Exporter.

## Acesso

Grafana esta publicado em `https://www.ecosologic.com.br/grafana/`. Prometheus e Alertmanager permanecem restritos ao servidor e podem ser acessados por tunel SSH:

```bash
ssh -p 46648 -L 3000:127.0.0.1:3000 -L 9090:127.0.0.1:9090 -L 9093:127.0.0.1:9093 hannibal@163.176.79.7
```

O Prometheus fica disponivel em `http://localhost:9090` e o Alertmanager em `http://localhost:9093` pelo tunel. A senha do Grafana fica somente no `.env` do servidor.

Os dados persistem nos volumes Docker `prometheus-data`, `alertmanager-data` e `grafana-data`.

## Alertas por e-mail (Prometheus -> Alertmanager -> SMTP OCI)

O Prometheus avalia as regras de `monitoring/alerts.yml` e encaminha os alertas ao
Alertmanager (`alertmanager:9093`), que os agrupa e envia por e-mail via SMTP da OCI
Email Delivery (porta 587, TLS obrigatorio).

Rotas por severidade: alertas `critical` vao para o receiver `critical` (espera de
10s e repeticao de 1h); demais severidades caem no receiver `warning` (repeticao de
4h). Ambos enviam para o destinatario definido por `ALERT_SMTP_TO`.

### Variaveis de ambiente (ficam somente no `.env` remoto)

| Variavel | Descricao |
| --- | --- |
| `ALERT_SMTP_HOST` | Host do SMTP OCI (sem porta) |
| `ALERT_SMTP_PORT` | Porta do SMTP (587) |
| `ALERT_SMTP_FROM` | Remetente dos e-mails de alerta |
| `ALERT_SMTP_TO` | Destinatario dos e-mails de alerta |
| `ALERT_SMTP_USERNAME` | Usuario de autenticacao SMTP |
| `ALERT_SMTP_PASSWORD` | Senha de autenticacao SMTP |

Nenhum valor real e armazenado no repositorio: `monitoring/alertmanager.yml` contem
apenas placeholders `${ALERT_SMTP_*}`. O Alertmanager nao possui a flag
`--config.expand-env` (essa e uma flag do Prometheus); por isso o container usa
`monitoring/alertmanager-entrypoint.sh`, que expande os placeholders a partir do
ambiente do container antes de iniciar o processo.

### Validacao

Para validar a configuracao sem subir o stack (sem deploy remoto), rode no servidor:

```bash
cd /home/hannibal/ecosologic
sh monitoring/validate.sh
```

O script executa `docker compose config`, `promtool check config`, `promtool check rules`
e `amtool check-config` usando as mesmas imagens pinadas do stack. Por seguranca, ele
**forca valores dummy** para todos os segredos (sobregravando qualquer valor real do
ambiente ou do `.env`, para que `docker compose config` nunca vaze segredos), usa `umask
077` e grava a config do Alertmanager expandida em um diretorio temporario privado
(`mktemp -d`, mode 700) removido por um `trap` na saida — nenhuma credencial fica em
`/tmp`.

## Jobs agendados (Hangfire)

O agendamento de notificacoes do CRM agora roda no Hangfire, com armazenamento persistente no
PostgreSQL (schema `hangfire`), substituindo o antigo `CrmNotificationWorker` (BackgroundService).
Nao existem mais `CrmNotificationWorker`, `CrmNotificationLoop` nem a chave
`Crm:NotificationSyncIntervalSeconds`. O comportamento anterior e preservado: o job continua
chamando `CrmNotificationService`, mantendo idempotencia e deduplicacao (nenhuma notificacao e
duplicada entre ciclos ou sob concorrencia).

Jobs recorrentes (IDs estaveis):

| ID | Cron (default) | Timezone | Chave de configuracao |
| --- | --- | --- | --- |
| `crm-notifications` | `*/5 * * * *` (5 min) | - | `Crm:NotificationCron` |
| `aneel-tariff-sync` | `0 3 1 * *` (dia 1, 03:00) | `America/Sao_Paulo` | `Tariffs:SyncCron` / `Tariffs:TimeZoneId` |

Retry e lock:

- CRM: `CrmNotificationJob` declara `AutomaticRetry` com 3 tentativas (atrasos 30s/60s/120s).
- ANEEL: tentativas configuradas por `Tariffs:MaxRetries` (default `3`, maximo `10`) com backoff
  exponencial limitado (inicio 60s, teto 3600s), aplicado somente ao job ANEEL via filtro global.
  A execucao recorrente e o disparo manual compartilham o lock distribuido `aneel-tariff-sync`
  (`DisableConcurrentExecution`, timeout de 10 min), impedindo sobreposicao mesmo com multiplas
  instancias do servidor.

Sincronizacao ANEEL (auditoria e observabilidade):

- Cada execucao e auditada na tabela `aneel_tariff_imports` (status `Running` -> `Succeeded`/`Failed`,
  contadores de registros lidos/aceitos/rejeitados/inseridos/encerrados/inalterados, URL e hash da
  fonte e erro sanitizado). Nenhum token, payload ou credencial aparece em logs ou mensagens de erro.
- Uma falha externa (ANEEL indisponivel, formato alterado ou resposta incompleta) marca a execucao
  como `Failed`, reapresenta a excecao para retry do Hangfire e preserva a ultima tarifa valida —
  nunca publica valor inventado nem remove a versao vigente.

O Dashboard Hangfire (`/hangfire`, somente papel `Admin`) exibe os jobs recorrentes, as execucoes
e as falhas. Ver `ops/production.md` para operacao, acesso ao Dashboard e recuperacao.
