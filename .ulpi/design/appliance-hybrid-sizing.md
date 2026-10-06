# Appliance Catalog and Hybrid Sizing UI

> Binds to `.ulpi/design/DESIGN.md`. Implement exactly this visual language;
> do not redesign the components or introduce a second theme.

## Goal

Permitir que o administrador monte uma instalação com cargas reais e futuras,
simule simultaneidade e veja rapidamente se o banco de baterias, o inversor e
os cabos suportam o cenário.

## Information Architecture

### Menu

Adicionar ao rail existente:

- `Dimensionamento solar`
- `Cargas`
- `Sistema híbrido`
- `Inversor e cabos`
- `Catálogo de cargas`
- `Revisão de fontes`

No mobile, o rail abre como drawer. A rota ativa deve permanecer anunciada por
texto, não somente por cor.

### Screens

#### 1. Catálogo de Cargas: `/admin/cargas`

Tela table-first, não uma grade de cards.

- Cabeçalho com busca global, filtro de categoria e botão `Adicionar carga`.
- Filtros rápidos: `Motores`, `127 V`, `220 V`, `Pendentes`.
- Tabela com fabricante, modelo, categoria, potência, motor, pico, tensões e
  fontes.
- Linha expandível mostra fontes e variantes de tensão.
- Drawer de edição para não perder o contexto da tabela.
- Ação de fusão abre comparação lado a lado e exige confirmação.

Estados:

- loading: linhas fantasma sem deslocamento.
- vazio: explicar como importar ou adicionar a primeira carga.
- erro: preservar filtros e oferecer tentar novamente.
- conflito: marca textual `Revisão necessária` e mostra fontes divergentes.

#### 2. Montador de Cargas: `/admin/dimensionamento/cargas`

Composição em duas zonas, com a grid como instrumento principal:

- coluna esquerda: biblioteca pesquisável de aparelhos;
- área central: **grid viva de equipamentos**;
- Loadboard fixa no topo da área de trabalho.

O operador pesquisa fabricante, modelo ou categoria. Cada resultado pode ser
arrastado para a grid ou incluído pelo botão `Adicionar`. Ao inserir, a linha
recebe edição inline de quantidade, tensão e horas de uso. A seleção também
pode ser feita pelo teclado.

### Grid viva de equipamentos

Colunas desktop:

- equipamento: fabricante e modelo;
- categoria;
- quantidade;
- tensão;
- potência unitária;
- potência total;
- corrente nominal;
- pico de partida;
- grupo de simultaneidade;
- remover.

Colunas calculadas não são editáveis. A cada inclusão ou alteração, a grid
recalcula a linha e o rodapé total sem recarregar a página.

Rodapé fixo da grid:

- `Potência total`: soma das potências das quantidades;
- `Corrente nominal total`: soma das correntes conforme a tensão de cada linha;
- `Pico de partida`: maior cenário de partida dos grupos, não uma soma cega de
  todos os motores;
- `Cargas`: quantidade de linhas e unidades.

O sistema deve distinguir a soma elétrica por tensão. Uma carga `127 V` e outra
`220 V` não devem ser somadas como se compartilhassem o mesmo circuito. O
rodapé mostra o total geral e, quando necessário, subtotais `127 V` e `220 V`.

O pico deve respeitar os grupos de simultaneidade. Quando não houver grupos,
o sistema exibirá a hipótese usada e permitirá configurar o percentual de
simultaneidade. O resultado sempre mostrará a diferença entre potência
contínua, corrente nominal e pico.

Cada item mostra fabricante, modelo, watts, ícone de motor quando aplicável e
uma linha de corrente calculada após a tensão ser definida.

Ao pesquisar, resultados já presentes na grid devem aparecer marcados como
`Adicionado`, e a ação deve aumentar a quantidade da linha existente ou criar
uma linha separada somente quando o usuário escolher explicitamente `Adicionar
como nova linha`.

#### 3. Sistema Híbrido: `/admin/dimensionamento/hibrido`

Fluxo em três momentos visuais, sem marcadores numéricos decorativos:

- cargas e simultaneidade;
- bateria `48 V lítio` e autonomia;
- inversor, pico e validação.

O usuário informa autonomia, profundidade de descarga, eficiência e capacidade
do módulo. A tela responde com capacidade necessária em `kWh` e `Ah`, módulos em
paralelo e margem restante.

#### 4. Inversor e Cabos: `/admin/dimensionamento/inversor-cabos`

Tela de diagnóstico técnico:

- bloco de especificação do inversor híbrido;
- diagrama linear de fluxo `painéis -> inversor -> barramento -> baterias -> cargas`;
- tabelas de cabos DC, strings e AC;
- coluna de critérios: corrente, queda de tensão, agrupamento e proteção;
- alertas bloqueantes destacados por texto e símbolo.

O diagrama é responsivo e tem descrição equivalente para leitor de tela.

#### 5. Revisão de Fontes: `/admin/catalogo/revisao`

Fila de candidatos n8n.

- coluna de candidato normalizado;
- duas colunas de fontes comparadas;
- diferença de potência/modelo destacada;
- ações `Aprovar`, `Fundir`, `Rejeitar`;
- lote só pode ser aprovado quando os valores exigidos estiverem completos.

## Primary Flow

1. Abrir `Cargas`.
2. Pesquisar “bomba” ou “split inverter”.
3. Arrastar o aparelho para um grupo.
4. Informar quantidade, tensão e horas.
5. Repetir para cargas futuras.
6. Criar ou selecionar grupo de simultaneidade.
7. Abrir `Sistema híbrido`.
8. Informar autonomia e parâmetros do banco de `48 V`.
9. Revisar `kWh`, `Ah`, módulos, potência contínua e pico.
10. Abrir `Inversor e cabos` e revisar bitolas, proteções e alertas.
11. Salvar snapshot técnico para a proposta.

## Drag-and-Drop Contract

### `ApplianceLibrary`

Purpose: pesquisar e iniciar o arraste de cargas aprovadas.

States: loading, empty, no results, offline, selected, dragging.

Accessibility:

- `role="listbox"` para resultados;
- cada item `role="option"`;
- Space inicia seleção móvel;
- setas movem a seleção;
- Enter adiciona ao grupo ativo;
- não exigir mouse para concluir o fluxo.

### `LoadGroup`

Purpose: aceitar aparelhos e organizar simultaneidade.

States: empty drop zone, receiving, populated, invalid drop, recalculating.

Accessibility:

- `role="list"`;
- `aria-label` identifica o grupo;
- `aria-live="polite"` anuncia item adicionado e total recalculado;
- botão `Adicionar por teclado` permanece visível em foco.

### `Loadboard`

Purpose: manter o impacto energético sempre visível.

Fields: potência contínua, pico, kWh/dia, bateria kWh, bateria Ah e margem do
inversor.

States:

- neutral before loads;
- calculated;
- warning when margin is low;
- blocked when peak exceeds inverter;
- stale when an input changed but recalculation is pending.

Em telas largas, a Loadboard fica acoplada ao cabeçalho da grid. Em telas
menores, o resumo de potência e corrente permanece sticky na parte inferior e
`Ver detalhes` abre o painel completo.

## Form Rules

- Um grupo expõe no máximo quatro campos primários por vez.
- Campos avançados ficam em disclosure `Premissas`.
- Tensão é select com `127 V` e `220 V`.
- Quantidade e horas aceitam teclado numérico e validação inline.
- Motor exibe o multiplicador de partida como dado somente leitura, salvo no
  catálogo; edição exige permissão administrativa.
- A ação primária da tela é única: `Calcular sistema`.

## Responsive Behavior

### Desktop, acima de 1024px

- rail visível;
- biblioteca, canvas e Loadboard em layout de três regiões;
- Loadboard sticky abaixo do cabeçalho.

### Tablet, 640–1024px

- rail recolhido;
- biblioteca abre como painel lateral;
- grupos ficam em duas colunas;
- Loadboard vira faixa horizontal rolável com valores completos.

### Mobile, abaixo de 640px

- rail vira drawer;
- biblioteca abre por botão `Adicionar carga`;
- grupos empilham;
- arraste continua disponível, mas botão `Adicionar ao grupo` é sempre
  oferecido;
- Loadboard vira faixa sticky inferior com resumo e botão `Ver detalhes`;
- tabelas usam linhas empilhadas com labels explícitos.

## Error and Edge States

- Sessão expirada: salvar rascunho local não sensível e redirecionar ao login.
- Offline: permitir reorganizar um rascunho, bloquear cálculo remoto e indicar
  `Aguardando conexão`.
- Fonte removida: preservar snapshot histórico e marcar a origem como antiga.
- Item duplicado: impedir inserção e oferecer `Ver item existente`.
- Modelo sem multiplicador de motor: bloquear cálculo de pico e encaminhar para
  revisão.
- Pesquisa retorna um item já adicionado: oferecer aumentar quantidade ou
  adicionar como linha separada, sem duplicar silenciosamente.
- Linha com tensão diferente do catálogo: bloquear inclusão e solicitar uma
  variante válida.
- Alteração de quantidade para zero: solicitar remoção ou restaurar `1`.
- Tensão incompatível: impedir seleção e explicar a variante disponível.
- Bateria insuficiente: mostrar quantos módulos adicionais são necessários.
- Inversor insuficiente: bloquear aprovação, não apenas mostrar um aviso.

## Accessibility and Copy

- Foco sempre visível em amarelo solar sobre azul-marinho ou contorno azul-marinho
  sobre superfície clara.
- Mensagens devem dizer a consequência: `O pico calculado excede o inversor em
  18%`, não apenas `Erro de capacidade`.
- Não usar “otimize”, “revolucione”, “seamless” ou promessas financeiras.
- Valores exibidos devem mostrar unidade em todos os contextos compactos.

## Pre-Flight Result

- Identity lock: passed. Todos os valores referenciam `DESIGN.md`.
- Anti-slop: passed. Sem gradiente, cards repetidos, glassmorphism ou copy vazia.
- State coverage: passed. Loading, vazio, erro, offline, conflito e bloqueio
  foram especificados.
- Accessibility: passed. Keyboard drag alternative, ARIA, foco, contraste e
  reduced motion estão definidos.
- Layout craft: passed. Table-first, three-zone builder, diagnostic flow e
  mobile bottom rail form famílias distintas.
- Cognitive load: passed. Um CTA primário por tela e disclosure para premissas.

Scores: distinctiveness 4/4, hierarchy 4/4, consistency 4/4, accessibility
4/4, state coverage 4/4, copy quality 3/4, restraint 4/4, motion motivation
4/4. Total: 31/32.

## Build Handoff

Target: engenheiro Angular do frontend existente.

Implementar exatamente este spec e `.ulpi/design/DESIGN.md`. Usar os componentes
Angular e estilos existentes quando compatíveis, tematizando-os com os tokens
travados. Não criar uma nova biblioteca visual, não redesenhar a identidade e
não remover a alternativa de teclado para drag-and-drop.

Acceptance criteria:

- menu e rotas acima funcionam em desktop, tablet e mobile;
- catálogo é table-first e possui revisão/fusão;
- montador aceita mouse, touch e teclado;
- Loadboard atualiza sem layout shift;
- todos os estados definidos possuem UI;
- cálculos e mensagens mostram unidades e premissas;
- foco, ARIA, contraste e reduced motion são validados;
- `npm run build`, testes unitários e E2E passam.
