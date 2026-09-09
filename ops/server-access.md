# Acesso ao servidor

## Dados

- Host: `163.176.79.7`
- Usuario: `hannibal`
- SSH: `46648/TCP`
- Knock sequence: `1230/TCP`, `5688/UDP`, `9112/TCP`

## Acesso

Execute o knocking na ordem abaixo, a partir de uma rede autorizada:

```bash
knock 163.176.79.7 1230:tcp 5688:udp 9112:tcp
ssh -p 46648 hannibal@163.176.79.7
```

Alternativa com `ncat` para o pacote UDP:

```bash
ncat -z -w 1 163.176.79.7 1230
ncat -u -z -w 1 163.176.79.7 5688
ncat -z -w 1 163.176.79.7 9112
ssh -p 46648 hannibal@163.176.79.7
```

Nenhuma senha ou chave privada deve ser armazenada neste arquivo. Use uma chave SSH protegida localmente ou um agente SSH.
