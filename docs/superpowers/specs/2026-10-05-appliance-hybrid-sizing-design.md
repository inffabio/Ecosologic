# Catálogo de Cargas e Dimensionamento Híbrido

## Objetivo

Criar uma base confiável de eletrodomésticos e equipamentos elétricos para
permitir que o dimensionamento solar considere cargas atuais e futuras, com
suporte posterior a sistemas híbridos de baterias e inversores.

O catálogo deve atender residências pequenas e grandes, lojas, padarias,
salões de festas e outras instalações comerciais, com atenção especial a
bombas d'água e equipamentos de ar-condicionado.

## Escopo

### Incluído

- Catálogo separado dos materiais fotovoltaicos existentes.
- Fabricante, modelo, categoria e potência nominal em watts.
- Identificação de equipamentos com motor.
- Multiplicador de corrente de partida para motores.
- Variantes de tensão de `127 V` e `220 V` sem duplicar o aparelho.
- Seleção de cargas em dimensionamentos residenciais e comerciais.
- Cálculo de corrente nominal e corrente de partida.
- Dimensionamento futuro de banco de baterias de `48 V` lítio em paralelo.
- Seção específica para dimensionamento de inversor híbrido.
- Pré-dimensionamento de cabos e proteções para o sistema completo.
- Workflow n8n semanal para descoberta e atualização do catálogo.
- Validação cruzada de fontes e revisão administrativa de divergências.

### Fora do escopo inicial

- Cadastro de preços de aparelhos.
- Compra automática de equipamentos.
- Cálculo de consumo real medido por telemetria.
- Publicação de tabelas completas de normas ABNT protegidas por direitos autorais.
- Substituição do projeto elétrico ou da responsabilidade de profissional
  habilitado.

## Decisões de Produto

### Potência e corrente

O catálogo armazenará a potência nominal informada pelo fabricante. Durante o
cálculo, o usuário escolherá a tensão da carga:

```text
correnteNominalA = potenciaW / tensaoV
```

Para equipamentos com motor:

```text
correntePartidaA = correnteNominalA * multiplicadorPartida
```

O sistema não deve inventar potência nem multiplicador. Registros sem fonte
confiável ficam pendentes para revisão.

### Tensão

Um aparelho representa o modelo e a potência. As tensões disponíveis ficam em
variantes relacionadas ao aparelho. Portanto, o mesmo modelo em `127 V` e
`220 V` não cria registros duplicados.

### Banco de baterias

O escopo aprovado assume banco de baterias de `48 V` lítio, com módulos ligados
em paralelo. A autonomia será informada pelo usuário, sem valor padrão fixo.

O modelo deve manter os parâmetros necessários para futuras extensões, como
profundidade de descarga, eficiência, capacidade unitária e quantidade de
módulos em paralelo, mas não deve permitir selecionar outras tecnologias ou
tensões no primeiro fluxo.

### Simultaneidade

O usuário poderá:

- definir grupos de cargas que operam simultaneamente;
- informar um percentual de simultaneidade para cargas não agrupadas;
- informar quantidade, tensão, horas de uso e autonomia desejada.

O resultado deve separar potência contínua de potência de pico.

## Modelo de Dados

### `electrical_appliances`

Tabela principal de aparelhos aprovados.

- `Id`: GUID.
- `Category`: categoria funcional.
- `Manufacturer`: fabricante exibido.
- `Model`: modelo exibido.
- `NormalizedManufacturer`: fabricante normalizado para deduplicação.
- `NormalizedModel`: modelo normalizado para deduplicação.
- `PowerW`: potência nominal positiva.
- `IsMotor`: indica carga com corrente de partida relevante.
- `StartingCurrentMultiplier`: multiplicador positivo quando `IsMotor` for verdadeiro.
- `Status`: registro aprovado e disponível para cálculo.
- `CreatedAt` e `UpdatedAt`.

Categorias iniciais:

- bomba centrífuga;
- bomba submersível;
- bomba pressurizadora;
- bomba periférica;
- bomba autoescorvante;
- bomba de piscina;
- ar-condicionado split convencional;
- ar-condicionado split inverter;
- ar-condicionado de janela;
- ar-condicionado portátil;
- ar-condicionado cassete;
- ar-condicionado piso-teto;
- geladeira;
- freezer;
- expositor e câmara fria;
- forno, estufa e micro-ondas;
- equipamento de padaria;
- máquina de lavar, secadora e lava-louças;
- chuveiro, torneira e aquecedor elétrico;
- televisão e informática;
- iluminação;
- som e eventos;
- máquina de gelo;
- outros equipamentos comerciais.

### `electrical_appliance_voltage_variants`

Variantes elétricas do aparelho.

- `Id`: GUID.
- `ApplianceId`: chave estrangeira.
- `VoltageV`: `127` ou `220`.
- `SourceCount`: quantidade de fontes que confirmam a variante.

Deve existir índice único em `(ApplianceId, VoltageV)`.

### `electrical_appliance_sources`

Fontes usadas para comprovar um aparelho.

- `Id`: GUID.
- `ApplianceId`: chave estrangeira.
- `Url`.
- `SourceType`: fabricante, distribuidor ou varejista.
- `CollectedAt`.
- `ExtractedManufacturer`.
- `ExtractedModel`.
- `ExtractedPowerW`.
- `Confidence`.

Várias fontes devem ser consolidadas no mesmo aparelho, sem criar cópias.

### `electrical_appliance_candidates`

Registros extraídos pelo n8n antes da publicação.

- Dados normalizados extraídos.
- URLs e tipo das fontes.
- Fingerprint de deduplicação.
- Quantidade de fontes concordantes.
- `Status`: `Pending`, `Approved`, `Rejected` ou `Merged`.
- Motivo da rejeição ou fusão.
- Data da última coleta.

## Deduplicação

A deduplicação ocorrerá no n8n e também na API/banco.

### Normalização

- Converter para maiúsculas.
- Remover acentos.
- Normalizar espaços e pontuação.
- Tratar hífen, espaço e ausência de separador como equivalentes em modelos.
- Separar informações de tensão do nome do modelo.
- Remover descrições comerciais que não fazem parte do modelo.

Exemplos equivalentes:

```text
ABC-09
ABC 09
ABC09
```

### Chave de identidade

A identidade lógica será composta por:

```text
categoria + fabricanteNormalizado + modeloNormalizado + potenciaW
```

A tensão não fará parte da identidade.

O banco terá índice único para impedir duplicatas mesmo quando duas execuções
do workflow ocorrerem ao mesmo tempo.

Modelos parecidos, mas não idênticos, devem ser encaminhados para revisão em
vez de serem automaticamente fundidos.

## Dimensionamento Híbrido

### Entradas

Para cada carga selecionada:

- aparelho;
- quantidade;
- tensão `127 V` ou `220 V`;
- horas de uso;
- grupo de simultaneidade opcional;
- percentual de simultaneidade quando não houver grupo.

Para o sistema:

- autonomia desejada;
- banco fixo de `48 V` lítio;
- profundidade de descarga;
- eficiência do banco e do inversor;
- capacidade de cada módulo de bateria;
- margem de projeto.

### Resultados

O cálculo deve apresentar:

- consumo por aparelho e total em `Wh` e `kWh`;
- potência contínua simultânea;
- corrente nominal por carga;
- corrente de partida por carga com motor;
- pico total estimado;
- energia necessária para a autonomia;
- capacidade de bateria em `kWh`;
- capacidade equivalente em `Ah` a `48 V`;
- quantidade de módulos em paralelo;
- potência contínua mínima do inversor;
- potência de pico mínima do inversor;
- alertas de incompatibilidade ou margem insuficiente.

Fórmulas-base:

```text
energiaCargasWh = potenciaW * quantidade * horasUso
energiaBateriaKWh = energiaAutonomiaKWh / (DoD * eficiencia)
capacidadeAh = energiaBateriaKWh * 1000 / 48
potenciaInversorW >= potenciaSimultaneaW
picoInversorW >= picoPartidaTotalW
```

O cálculo deverá informar claramente as premissas usadas e não substituir o
dimensionamento elétrico final.

## Cabos e Proteções

O pré-dimensionamento cobrirá:

- cabos DC entre módulos de bateria, barramentos e inversor;
- cabos DC dos painéis e strings;
- entrada e saída AC do inversor;
- circuitos AC das cargas.

Os critérios considerados serão:

- capacidade de condução de corrente;
- queda de tensão;
- temperatura ambiente;
- método de instalação;
- agrupamento de circuitos;
- corrente de curto-circuito;
- fusível ou disjuntor compatível;
- polaridade e identificação dos condutores.

As referências normativas a validar no desenvolvimento serão principalmente
ABNT NBR 5410 para instalações elétricas de baixa tensão e ABNT NBR 16690 para
instalações fotovoltaicas. Requisitos específicos do fabricante do inversor e
da bateria prevalecem quando forem mais restritivos.

O resultado exibirá seção calculada, seção comercial recomendada, proteção,
queda de tensão e alertas. As normas serão usadas como critério de cálculo,
sem reproduzir tabelas protegidas no sistema ou na documentação pública.

## Workflow n8n

### Frequência

Execução semanal, com possibilidade de execução manual para reprocessamento.

### Fluxo

```text
Trigger semanal
  -> gerar consultas por categoria
  -> Tavily Search
  -> Brave Search como fallback
  -> abrir páginas relevantes
  -> extrair dados com 9Router
  -> normalizar fabricante, modelo e potência
  -> calcular fingerprint
  -> agrupar fontes concordantes
  -> enviar candidatos para a API
  -> publicar somente candidatos validados
  -> registrar pendências e divergências
```

O workflow reutilizará o broker de segredos existente. O n8n não acessará o
PostgreSQL diretamente. A API será responsável por autenticação, validação,
deduplicação final e transação de gravação.

### Regra de publicação

- Duas fontes independentes concordantes em fabricante, modelo e potência:
  publicação automática.
- Uma única fonte: `Pending`.
- Potência ou modelo divergente: `Pending`.
- Registro equivalente já aprovado: consolidar nova fonte no registro existente.
- Registro parecido, mas não comprovadamente igual: `Pending` para revisão.

O sistema armazenará URLs, datas, valores extraídos e confiança para auditoria.

## API e Interface

A API deverá fornecer:

- busca paginada de aparelhos aprovados;
- filtro por categoria, fabricante, motor e potência;
- consulta de variantes de tensão;
- consulta e revisão de candidatos;
- aprovação, rejeição e fusão de duplicatas;
- endpoint protegido para importação do n8n;
- endpoint de pré-dimensionamento híbrido.

O painel administrativo deverá permitir revisar divergências, comparar fontes,
fundir candidatos e editar multiplicador de partida antes da aprovação.

A tela de dimensionamento deverá permitir adicionar aparelhos futuros sem
alterar o catálogo. Cada dimensionamento guardará um snapshot das cargas e das
premissas utilizadas.

## Validação e Erros

- Potência nula ou negativa é rejeitada.
- Multiplicador de partida é obrigatório para motores publicados.
- Tensão deve ser `127` ou `220 V` no primeiro fluxo.
- Carga sem fonte confiável não é publicada.
- Aparelhos duplicados são rejeitados ou consolidados.
- Capacidade de inversor abaixo da carga contínua gera erro de dimensionamento.
- Pico de partida acima da capacidade do inversor gera alerta bloqueante.
- Queda de tensão acima do limite configurado gera alerta.
- Falta de dados suficientes para cabo ou proteção gera resultado inconclusivo,
  nunca uma bitola inventada.

## Testes e Aceitação

### Backend

- Testes de normalização e fingerprint.
- Testes de deduplicação concorrente.
- Testes de fusão de fontes.
- Testes de variantes `127/220 V` no mesmo aparelho.
- Testes de corrente nominal e pico de motor.
- Testes de `kWh`, `Ah`, autonomia e módulos em paralelo.
- Testes de potência contínua e pico do inversor.
- Testes de dimensionamento de cabos e proteções.
- Testes de importação idempotente do n8n.

### Frontend

- Busca e filtro de aparelhos.
- Inclusão de cargas futuras no dimensionamento.
- Seleção de tensão e quantidade.
- Grupos de simultaneidade.
- Exibição de premissas, resultados e alertas.
- Revisão e fusão de candidatos no painel administrativo.
- Layout funcional em desktop e mobile.

### Operação

- Executar o workflow semanal em ambiente controlado.
- Confirmar que duas fontes concordantes publicam um registro.
- Confirmar que divergência fica pendente.
- Confirmar que a segunda execução não cria duplicatas.
- Confirmar auditoria das fontes e da data de coleta.
- Validar produção com cargas de residência, bomba, ar-condicionado e padaria.

## Critério de Pronto

O recurso será considerado pronto quando:

- o catálogo aceitar e pesquisar aparelhos aprovados;
- o n8n conseguir coletar, normalizar e importar candidatos;
- duplicatas forem bloqueadas no n8n, API e banco;
- aparelhos `127/220 V` permanecerem em um único registro;
- o dimensionamento calcular corrente, consumo, autonomia, `kWh`, `Ah` e
  módulos em paralelo;
- o módulo de inversor validar potência contínua e pico;
- o pré-dimensionamento indicar cabos, proteções e alertas;
- os testes backend/frontend e o workflow controlado passarem;
- a interface deixar explícita a necessidade de validação profissional do
  projeto elétrico final.
