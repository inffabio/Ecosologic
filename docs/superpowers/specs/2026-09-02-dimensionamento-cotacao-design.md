# Especificacao: Dimensionamento, Cotacao e Proposta

## Objetivo

Criar uma ferramenta interna no CRM para transformar um lead em dimensionamento
tecnico, cotacao comercial e proposta. O calculo nao sera exibido diretamente ao
cliente. A proposta sera gerada pela equipe a partir dos resultados aprovados.

Fluxo principal:

```text
Lead -> Dados da fatura -> Dimensionamento -> Cotacao -> Proposta
```

## Fontes de referencia

- `PlanilhaDimensionamento750.xlsx`: referencia para formulas, entradas, custos,
  analise financeira e projecao.
- `Orcamento705kwh_Eliezer.indd`: referencia visual e estrutural da proposta.
- Tarifas oficiais e regras vigentes da ANEEL, Light RJ e Enel Distribuicao Rio.

A planilha possui referencias externas quebradas. Essas dependencias serao
substituidas por tabelas internas versionadas. O arquivo INDD e binario nativo;
para reproduzir textos e elementos com fidelidade sera necessario exportacao em
PDF ou IDML, caso os previews e o conteudo recuperado nao sejam suficientes.

## Entradas do dimensionamento

### Cliente e unidade consumidora

- Lead relacionado.
- Nome, documento e contatos.
- Endereco completo.
- Concessionaria: `Light RJ` ou `Enel RJ`.
- Coordenadas ou municipio para irradiacao e temperatura.
- Data da analise e data prevista de conexao.
- Unidade consumidora existente ou nova.

### Fatura

- Grupo tarifario: A ou B.
- Subgrupo/classe: B1, B2, B3, B4, A1, A2, A3, A3a, A4 ou AS.
- Modalidade: convencional, branca, azul ou verde.
- Tipo de ligacao: mono, bi ou trifasica.
- Consumo dos ultimos 12 meses.
- Consumo por posto tarifario quando aplicavel.
- Demanda contratada e medida quando aplicavel.
- TE, TUSD, TUSD Fio B e demais componentes informados na fatura.
- ICMS, PIS, COFINS e outros encargos aplicaveis.
- Energia compensada, injetada e creditos existentes, quando disponiveis.
- Percentual ou valor de disponibilidade informado pela regra tarifaria.

### Local e sistema

- Orientacao e inclinacao dos modulos.
- Sombreamento.
- Area disponivel e restricoes do telhado.
- Modulo selecionado, potencia, eficiencia e garantias.
- Inversor(es), potencia, faixa MPPT, tensao e corrente.
- Limites de strings e quantidade de modulos por string.
- Perdas de sujeira, tolerancia, mismatch, temperatura, CC, MPPT, inversor e CA.
- Fator de sobredimensionamento desejado.

## Motor tarifario

O motor nao podera assumir B1 como padrao. A tabela tarifaria sera selecionada
por concessionaria, grupo, subgrupo, modalidade, posto e vigencia.

### Grupo B

Suportar B1 residencial, B2 rural, B3 demais classes e B4 iluminacao publica,
incluindo modalidade convencional e tarifa branca quando disponivel. Calcular
consumo, disponibilidade, energia compensavel, tributos e saldo da fatura.

### Grupo A

Suportar A1, A2, A3, A3a, A4 e AS, modalidades azul e verde, demanda contratada,
demanda medida, ultrapassagem e consumo ponta/fora ponta. Cada posto deve ser
calculado separadamente antes da consolidacao mensal.

### Fio B

O Fio B sera uma regra parametrizada, nunca uma constante espalhada no codigo.
Cada versao deve registrar:

- Concessionaria.
- Vigencia inicial e final.
- Grupo/subgrupo e modalidade.
- Posto tarifario.
- Base tarifaria usada, especialmente componente TUSD de distribuicao.
- Percentual progressivo aplicavel por ano conforme Lei 14.300/2022 e atos
  regulatorios vigentes.
- Tratamento da energia compensada, disponibilidade e tributos.
- Fonte oficial e data de revisao.

O ano de referencia integra a identidade da regra de compensacao: regras de anos
diferentes podem coexistir mesmo com vigencia sobreposta, e a selecao por
combinacao (concessionaria, grupo, subgrupo, modalidade, posto) e vigencia deve
informar o ano de referencia. Duplicidade ou sobreposicao de vigencia so e
rejeitada quando a combinacao e o ano de referencia coincidem.

O endpoint de selecao (`GET /api/tariffs/grid-rules`) retorna por padrao apenas
regras completas; `isComplete=false` permite listar regras incompletas/legadas
para auditoria.

O resultado devera separar claramente energia compensada, parcela sujeita ao Fio
B, tributos, disponibilidade e demais componentes. O sistema deve bloquear a
emissao quando faltar uma regra tarifaria vigente para a combinacao escolhida,
em vez de estimar silenciosamente.

## Calculo tecnico

Para cada mes:

1. Obter HSP da localizacao e periodo.
2. Aplicar fator K de temperatura/inclinacao conforme tabela versionada.
3. Aplicar perdas individuais e totalizar perdas.
4. Calcular geracao mensal estimada.
5. Calcular energia compensavel respeitando disponibilidade e regras da tarifa.
6. Calcular excedente, deficit e creditos.
7. Consolidar geracao anual, media mensal e pior mes.

Saidas tecnicas:

- Potencia nominal em kWp.
- Quantidade e modelo de modulos.
- Inversor(es) e potencia.
- Configuracao de strings.
- Geracao mensal e anual.
- Perdas detalhadas e totais.
- Cobertura do consumo.
- Energia excedente e deficit mensal.
- Alertas de tensao, corrente, area ou compatibilidade.
- Premissas e fonte de cada parametro.

O caso de referencia da planilha deve ser reproduzido como teste de regressao,
sem transformar seus valores em defaults comerciais. Os dados do cliente sempre
substituem os valores de referencia.

### Formulas e premissas implementadas (motor tecnico — Task 3)

O motor e deterministico e nao depende de banco, HTTP ou relogio. Toda entrada
mal formada e rejeitada com excecao (dimensoes, unidades, NaN/Infinity,
negativos); as incompatibilidades fisicas viram alertas no resultado (nao
impeditivos ou impeditivos), nunca excecoes.

A validacao de finitude e centralizada em um helper finito compartilhado
(`SolarSizingValidation.RequireFinite`), usado por modulos, perdas, series mensais
e fatores. Todos os intermediarios do calculo (HSPk, somas, kWp alvo/instalado,
geracao, cobertura, tensoes/correntes das strings) sao validados como finitos
antes de qualquer `ceil`/conversao e antes de comparacoes de alerta, evitando
overflow silencioso e comparacoes `Infinity > Infinity`. Em particular, na
validacao de tensao, os operandos (`MpptVoltageMax`, `MaxInputVoltage`, `Vmp`,
`Voc`) e os quocientes `MpptVoltageMax/Vmp` e `MaxInputVoltage/Voc` sao validados
como finitos e dentro do intervalo seguro de `int` ANTES da conversao para `int`
(nunca conversao silenciosa de `Infinity`/overflow). O resultado final
(`SolarSizingResult.Create`) tambem valida que todos os valores numericos sao
finitos antes de retornar.

Tipos:

- `SolarSizingInput` (Domain): entradas normalizadas e validadas.
- `SolarSizingResult` (Domain): saidas tecnicas e alertas.
- `SolarSizingCalculator` (Application): calculo; `EngineVersion = "1.0.0"`.

Constantes:

- `DaysPerMonth = 30` (mesmo `*30` da planilha, simplificacao de dias por mes).

Entradas e unidades:

- `MonthlyConsumptionKWh`, `MonthlyHsp`, `MonthlyKFactor`: exatamente 12 valores,
  nao negativos e finitos. HSP e a irradiacao diaria (kWh/m2/dia); K e o fator de
  correcao por latitude/inclinacao (aplicado multiplicativamente, como a aba
  "Correcao K").
- `Module` (potencia Wp, Voc, Vmp, Isc, Imp, area m2): todos positivos, com
  `Voc >= Vmp`, `Isc >= Imp` e `PowerWp` coerente com `Vmp * Imp` dentro de
  tolerancia relativa documentada de 5% (`PowerCoherenceTolerance`).
- `Losses` (sombreamento, sujeira, tolerancia, mismatch, temperatura, CC, MPPT,
  inversor, CA): fracoes em [0, 1].
- `OversizingFactor`: > 0 (padrao 1.0). `DeratingFactor`: (0, 1] (padrao 1.0).
- `Orientation`/`InclinationDegrees` (0..90): premissas preservadas.
- `AvailableAreaM2`: > 0. `Inverter` (opcional): limites de tensao/corrente/MPPT,
  com `MpptCount` (rastreadores) e `MaxStringsPerMppt` (max de strings em paralelo
  por MPPT, padrao 1); a corrente maxima e por MPPT, somando as strings paralelas.
- `ModulesPerString`/`StringCount`: opcionais (>= 1).

Formulas:

1. `perdasTotais = soma` das perdas individuais (totalizacao aditiva, como a
   planilha: `=C30+C31+C32`).
2. `hspk[m] = HSP[m] * K[m]` (horas de sol pleno efetivas, diarias).
3. `geracao[m] = hspk[m] * kWpInstalado * 30 * (1 - perdasTotais) * derating`.
   O fator `(1 - perdasTotais)` e clampado em 0: perdas totais >= 100% zeram a
   geracao (nunca valores negativos) e emitem alerta impeditivo.
4. `geracaoAnual = soma(geracao[m])`; `geracaoMedia = geracaoAnual / 12`.
5. `kWpAlvo = (consumoMedioDiario / hspkMedio) * (1 + perdasTotais) * sobredim / derating`.
   `consumoMedioDiario = soma(consumo)/12/30`. O derating divide a meta (geracao
   alvo); como a geracao real multiplica por derating, nao ha dupla contagem.
6. `modulos = ceil(kWpAlvo * 1000 / potenciaModulo)` (arredondamento para cima,
   documentado; nao ha ajuste manual de modulos no motor). Intermediarios usam
   aritmetica finita em double e sao validados (nao finito ou fora do range int
   gera erro de validacao) antes do `ceil`/conversao para `int`.
7. `kWpInstalado = modulos * potenciaModulo / 1000`.
8. `compensavel[m] = min(geracao[m], consumo[m])` (tecnico; Fio B/disponibilidade
   ficam na Task 4). `cobertura = geracaoAnual / consumoAnual`.
9. `saldo[m] = geracao[m] - consumo[m]`; `excedente = soma(max(0, saldo))`;
   `deficit = soma(max(0, -saldo))`; pior mes = menor saldo (indice 1..12).

Configuracao eletrica (inversor): a tensao por string nao soma em paralelo; a
corrente soma. `stringsPorMppt = ceil(stringCount / mpptCount)`. Corrente por MPPT
= `stringsPorMppt * Imp` (e `* Isc`), comparada contra `MaxInputCurrent`. O total
de strings e limitado por `MpptCount * MaxStringsPerMppt`. O produto `modulesPerString
* stringCount` e calculado em `long` para evitar overflow.

Quando QUALQUER um de `modulesPerString` ou `stringCount` e fornecido, o motor
completa o valor ausente (`stringCount = ceil(moduleCount / modulesPerString)` ou
`modulesPerString = ceil(moduleCount / stringCount)`) e valida que o produto
`modulesPerString * stringCount` (em `long`) e EXATAMENTE igual ao `moduleCount`
dimensionado. Sobrealocacao ou subalocacao (produto maior ou menor) emitem o alerta
impeditivo `ConfiguracaoEletricaDivergente` (nao rejeita com excecao — incompatibilidade
fisica vira alerta, como as demais). A potencia reportada (kWp instalado) e SEMPRE
derivada do `moduleCount` dimensionado, nunca da configuracao eletrica; ou seja, uma
configuracao eletrica divergente nao altera a potencia nem a geracao reportadas.

Quando NENHUM dos dois e fornecido (configuracao automatica), o motor deriva uma
configuracao consistente: `modulesPerString` e o maior divisor de `moduleCount` que
respeita o limite de tensao do inversor (`min(floor(MpptVoltageMax/Vmp),
floor(MaxInputVoltage/Voc))`, minimo 1), e `stringCount = moduleCount /
modulesPerString`; assim o produto e sempre EXATAMENTE `moduleCount` (sem
sobre/subalocacao silenciosa). Sem inversor, `modulesPerString = moduleCount` e
`stringCount = 1`.

Alertas (codigos estaveis):

- Impeditivos: `PerdasTotaisAcimaDe100`, `SemGeracao`, `AreaInsuficiente`,
  `TensaoStringAcimaMpptMax`, `TensaoVocAcimaMaxInversor`, `NumeroStringsAcimaMppt`,
  `CorrenteAcimaMaxInversor`, `CorrenteCurtoAcimaMaxInversor`,
  `ConfiguracaoEletricaDivergente`.
- Nao impeditivos: `AreaApertada` (> 90% da area), `TensaoStringAbaixoMpptMin`,
  `FatorDimensionamentoGlobalForaDaFaixa` (Pcc/Pca fora de 0.75..1.30).

Caso de referencia (planilha `PlanilhaDimensionamento750.xlsx`):

- Consumo 750 kWh/mes; HSP `[6.18, 6.38, 5.15, 4.44, 3.59, 3.32, 3.35, 4.22, 4.41,
  5.08, 5.22, 6.05]`; K (lat 23, incl 10) `[0.99, 1.01, 1.05, 1.08, 1.10, 1.10,
  1.09, 1.07, 1.04, 1.01, 0.99, 0.98]`; modulo 700 Wp; perdas
  `[0, 0.02, 0, 0, 0.112, 0.01, 0.02, 0.04, 0.01]` (total 0.212); sobredim 1.0.
- Resultado: 9 modulos, 6.3 kWp, geracao media ~736.55 kWh/mes, anual ~8838.61 kWh.

Diferencas entre a planilha e o motor (documentadas):

1. **Temperatura**: a planilha calcula a perda por temperatura (`-coef * dT` por
   mes) com um VLOOKUP deslocado (Janeiro a Abril usam a coluna de Abril). O motor
   recebe a perda de temperatura como entrada (fracao em [0,1]). No caso de
   referencia usou-se 0.112 (media do Rio de Janeiro). Por isso a geracao media do
   motor (736.5507 kWh) difere -0.0333 kWh (-0,005%) da planilha (736.5840 kWh), e
   a anual (8838.6078 kWh) difere -0.4022 kWh (-0,005%) da planilha (8839.0100 kWh).
   A regressao usa tolerancia absoluta apertada (0.05 kWh na media, 0.5 kWh na
   anual) para acomodar exatamente essa divergencia documentada.
2. **Disponibilidade**: a planilha subtrai o custo de disponibilidade (100 kWh
   trifasico) antes de dimensionar; o motor nao subtrai (conceito tarifario/Fio B,
   Task 4). O kWp de referencia (6.3 kWp, 9 modulos) coincide porque o
   arredondamento para cima leva aos mesmos 9 modulos.
3. **Arredondamento**: a planilha usa `ROUNDUP(...)+ajusteManual`; o motor usa
   `ceil` sem ajuste manual.
4. **Markup de perdas**: o motor usa `(1 + perdasTotais)` no dimensionamento
   (aproximacao linear da planilha), nao o inverso exato `1/(1 - perdas)`; a
   geracao usa `(1 - perdasTotais)`. Essa assimetria e intencional para reproduzir
   a planilha e e documentada aqui.

### Formulas e premissas implementadas (Fio B e fatura — Task 4)

Tipos (Application): `FioBCalculator`, `TariffBillCalculator` (`EngineVersion =
"1.0.0"`), `FioBRequest`/`FioBResult`, `TariffBillInput`/`TariffBillResult`. O
motor recebe o perfil tarifario e as regras de Fio B JA RESOLVIDAS (por posto) e
as series mensais de consumo/injecao; nao depende de banco ou HTTP. Todos os
valores monetarios e de energia usam `decimal` (nao ha NaN/Infinity em decimal);
negativos sao rejeitados e overflow de multiplicacao propaga como excecao (nunca
vira zero silencioso).

Postos aplicaveis por modalidade: Convencional `[Single]`; Branca
`[Peak, Intermediate, OffPeak]`; Azul `[Peak, OffPeak]`; Verde `[Peak, OffPeak]`.

Compensacao por mes/posto (Sistema de Compensacao de Energia Eletrica):

1. `compensadoMesmoMes[p] = min(injecao[p], consumo[p])`.
2. Se `injecao > consumo`: `excedente[p] = injecao - consumo` vira credito com
   `ExpiresAfterMonth = mesGeracao + CreditExpiryMonths` (padrao 60, configuravel)
   e posto de origem preservado.
3. Se `consumo > injecao`: `deficit[p] = consumo - injecao` e abatido por creditos
   (FIFO por expiracao/geracao), incluindo excedente do proprio mes — isto realiza
   o posto cruzado (excedente fora ponta compensa deficit na ponta) com
   rastreabilidade via `PostBillLine.CompensatedViaCreditKWh` e `CreditEntry.Post`.
4. `energiaDaRede[p] = consumo[p] - compensadoMesmoMes[p] - creditoUsado[p]`;
   `compensado[p] = compensadoMesmoMes[p] + creditoUsado[p]`.

Grupo B (piso de disponibilidade):

- `piso = 30/50/100 kWh` (mono/bi/tri, via `TariffAvailability`).
- `disponibilidade = max(0, piso - energiaDaRedeTotal) × TE` (custo de
  disponibilidade incide sobre a TE; fora ponta quando branca).
- `TE = energiaDaRede × teRate`; `TUSD = energiaDaRede × tusdRate`;
  `tributos = energiaDaRede × taxRate + taxFixoMensal`. TUSD e tributos incidem
  sobre a energia efetivamente consumida da rede (nao sobre o piso).

Grupo A:

- `demanda = max(contratada, medida) × DEMAND`; `ultrapassagem = max(0, medida -
  contratada) × OVERAGE`. Demanda/ultrapassagem nao sao reduzidas pela energia
  solar nesta versao.
- Componentes `DEMAND`/`OVERAGE` em `KW`: usa o posto ponta quando presente, senao
  fora ponta, senao unico (simplificacao: a demanda fora ponta nao e faturada
  separadamente).

Fio B:

- `FioB = (ProgressivePercent / 100) × BaseComponente × EnergiaCompensada`, onde
  `BaseComponente` e o valor do componente indicado pela regra (`BaseComponent`)
  para o posto da regra. O Fio B NAO e tarifa universal: incide apenas sobre a base
  configurada e sobre a energia compensada (mesmo mes + creditos usados).

Resolucao da TUSD: componente `TUSD` combinado quando presente; senao a soma de
`TUSD_DISTRIBUTION + TUSD_TRANSMISSION`.

Arredondamento: centavos apenas no total mensal e na fatura anual
(`Math.Round(..., 2, MidpointRounding.AwayFromZero)`); as linhas intermediarias
(TE, TUSD, tributos, Fio B, disponibilidade, demanda, ultrapassagem) mantem a
precisao decimal completa. A fatura anual e a soma dos totais mensais ja
arredondados.

Bloqueio: perfil incompleto (`ProfileIncomplete`), regra de Fio B ausente
(`RuleMissing`) ou incompleta (`RuleIncomplete`) e componente necessario ausente
(`ComponentMissing`, incluindo a base do Fio B) retornam resultado bloqueado com
erros explicitos, sem meses e sem resumo — nunca zero silencioso.

Limitacoes documentadas:

- Tarifas de teste sao fixtures sinteticas; valores reais de Light/Enel nao sao
  embutidos. A fonte oficial e a vigencia permanecem no `TariffProfile`.
- Demanda azul: faturada apenas no posto ponta (nao separa ponta/fora ponta).
- Tributos modelados como `TAX` por `KWh` (taxa sobre a energia da rede) e `TAX`
  por `Mes` (fixo mensal); `TAX` por `KW` (sobre demanda) nao e modelado.
- Injecao solar e fornecida por posto como entrada da Task 4 (a Task 3 gera uma
  serie unica; a alocacao por posto e responsabilidade do chamador).
- Nao calcula cotacao/proposta (Tasks 6/7); apenas fatura sem/com solar, economia
  e creditos.

## Custos e cotacao

A cotacao sera independente do calculo tecnico, mas consumira seus resultados.
Cada item deve ter:

- Categoria.
- Descricao.
- Marca/modelo.
- Quantidade.
- Custo unitario.
- Preco unitario.
- Frete.
- Instalacao e servicos.
- Garantia.
- Custo total e preco total.

A composicao deve calcular separadamente:

- Subtotal de equipamentos.
- Materiais e cabos.
- Mao de obra.
- Projeto, homologacao e instalacao.
- Frete.
- Custo total.
- Margem e lucro.
- Impostos.
- Preco final.
- Condicoes de pagamento.
- Validade da cotacao.

Alteracoes manuais de margem, imposto ou preco exigem registro do usuario,
data, valor anterior e valor novo.

## Projecao financeira

Com base no motor tarifario e na cotacao, calcular:

- Fatura sem solar.
- Fatura estimada com solar.
- Custo do Fio B.
- Economia mensal e anual.
- Reajuste tarifario parametrizado.
- Degradacao dos modulos.
- Fluxo de caixa.
- Payback simples e descontado, se os parametros financeiros forem fornecidos.
- Projecao de ate 25 anos.

Toda projecao deve exibir as premissas utilizadas e a data da tarifa. Nao usar
promessas financeiras sem contexto ou sem indicar que sao estimativas.

## Modelo de dados interno

Entidades previstas:

- `SolarSizing`: entradas, premissas, resultados tecnicos e versao do motor.
- `TariffProfile`: concessionaria, grupo, modalidade, postos, vigencia e fontes.
- `SolarQuote`: itens, custos, margem, impostos, preco, validade e status.
- `Proposal`: cotacao vinculada, versao do template, dados renderizados, status e
  arquivo gerado.
- `ProposalTemplate`: identificacao da versao visual e campos disponiveis.

Cada dimensionamento, cotacao e proposta deve manter um snapshot dos parametros
usados. Alterar uma tarifa no futuro nao pode mudar uma proposta ja emitida.

## UX/UI interno

O painel deve conduzir o usuario por etapas claras:

1. Dados da unidade e fatura.
2. Dimensionamento tecnico.
3. Revisao de premissas e alertas.
4. Composicao da cotacao.
5. Revisao comercial.
6. Geracao e historico da proposta.

Requisitos de interface:

- Mostrar unidades em todos os campos.
- Diferenciar valor informado, calculado e editado manualmente.
- Mostrar a fonte e vigencia de tarifas.
- Impedir avancar com dados essenciais ausentes.
- Permitir salvar rascunho.
- Mostrar comparacao antes/depois ao alterar modulo, tarifa ou margem.
- Destacar alertas tecnicos sem depender somente da cor.
- Responsividade para notebook e tablet usados em atendimento.
- Exportacao e impressao com layout estavel.

## Proposta

A proposta deve ser gerada a partir de um template versionado, sem depender do
arquivo INDD em tempo de execucao. O template deve conter campos mapeados para:

- Cliente e endereco.
- Resumo do sistema.
- Geracao prevista.
- Consumo e economia estimada.
- Equipamentos e garantias.
- Investimento e condicoes de pagamento.
- Prazo, escopo, homologacao e instalacao.
- Monitoramento e pos-venda.
- Validade, premissas e observacoes regulatorias.

Antes da implementacao final do PDF, validar uma exportacao PDF/IDML do INDD
para confirmar textos, imagens, fontes, dimensoes e ordem das duas paginas.

## Casos de teste obrigatorios

- B1, B2, B3 e B4 com Light e Enel.
- Grupo A com tarifa azul e verde.
- Tarifa branca com ponta e fora ponta.
- Mono, bi e trifasico com disponibilidade correta.
- Fio B por ano de transicao.
- Regra tarifaria ausente bloqueando emissao.
- Consumo variavel nos 12 meses.
- Geracao menor que consumo no pior mes.
- Geracao excedente e creditos.
- Perdas alteradas recalculando resultados.
- Modulo/inversor incompativel gerando alerta impeditivo.
- Cotacao com margem, imposto, frete e pagamento.
- Recalculo sem alterar proposta ja emitida.
- Snapshot preservando tarifa e premissas historicas.
- PDF gerado com todos os campos obrigatorios.

## Ordem de implementacao

1. Modelos, status e snapshots.
2. Tabelas tarifarias Light/Enel e importacao versionada.
3. Motor tecnico com testes de regressao da planilha.
4. Motor de Fio B e calculo de fatura.
5. Tela interna de dimensionamento.
6. Composicao e revisao da cotacao.
7. Template de proposta e geracao de PDF.
8. Testes E2E do fluxo completo.

## Pendencias para validacao

- Obter tabelas tarifarias oficiais atuais de Light e Enel RJ.
- Confirmar regras especificas de enquadramento da unidade e data de conexao.
- Obter PDF ou IDML exportado do modelo Adobe.
- Confirmar escopo comercial, garantias, prazos e condicoes de pagamento.
- Definir se a proposta sera PDF, PDF e envio por e-mail, ou apenas arquivo para
  download interno na primeira versao.
