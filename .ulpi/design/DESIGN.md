# Ecosologic UI Design Language

## Design Read

Um painel de bancada elétrica premium: o operador monta uma instalação como quem
organiza instrumentos sobre uma mesa de trabalho, vendo carga, corrente e risco
antes de tomar uma decisão comercial.

## Locked Direction

**Technical / Utilitarian: Solar Instrument Panel**

Esta identidade é específica para a Ecosologic porque combina o azul-marinho
operacional já existente com amarelo solar como sinal técnico e verde apenas
como confirmação. A interface não usa o visual genérico de dashboard SaaS, não
usa gradientes, glassmorphism ou grades repetitivas de cards.

## Register

Product/admin surface dentro de uma marca técnica premium. O operador precisa
de densidade e precisão; o cliente precisa de explicações humanas e legíveis.

## Palette

Usar somente estes tokens, derivados do design existente:

```css
--ink: oklch(0.22 0.04 255);
--ink-strong: oklch(0.14 0.035 255);
--surface: oklch(0.98 0.01 95);
--surface-muted: oklch(0.94 0.018 95);
--solar: oklch(0.80 0.17 85);
--solar-strong: oklch(0.68 0.16 78);
--brand-green: oklch(0.54 0.16 157);
--body-text: oklch(0.35 0.035 255);
--muted-text: oklch(0.52 0.025 255);
--danger: oklch(0.52 0.18 27);
--warning: oklch(0.67 0.15 65);
```

No máximo 10% da área usa `--solar` ou `--brand-green` como acento. Alertas
devem usar texto e ícone além de cor.

## Typography

- Manrope: títulos, navegação, métricas e controles.
- Source Sans 3: ajuda contextual, descrições e tabelas longas.
- Nunca usar fontes monoespaçadas como atalho para “tecnologia”.
- Títulos usam `text-wrap: balance`.
- Corpo limita linhas a 65–75 caracteres quando houver texto corrido.

Escala:

- `0.72rem`: metadados curtos.
- `0.86rem`: controles e tabelas.
- `1rem`: texto base.
- `1.35rem`: títulos de seção.
- `clamp(1.65rem, 3vw, 2.7rem)`: título de tela.

## Shape, Spacing, Depth

- Raio padrão: `0px` para superfícies de dados e `2px` para controles.
- Raio de `6px` somente para drawers, menus e agrupamentos móveis.
- Escala de espaçamento: `4, 8, 12, 16, 24, 32, 48px`.
- Bordas completas de `1px` em `--ink` com baixa opacidade; sem listras laterais
  decorativas.
- Sombras mínimas. Elevação é comunicada por borda, posição e contraste, não
  por cartões flutuantes.

## Signature: Loadboard

Cada tela de dimensionamento possui uma faixa de leitura fixa chamada
**Loadboard**:

- potência simultânea;
- pico de partida;
- consumo diário;
- capacidade estimada de bateria;
- estado do inversor.

Os números são alinhados em uma régua horizontal com pequenos marcadores de
escala. A régua muda de estado apenas quando os cálculos mudam. Ela é o sinal
visual que liga catálogo, dimensionamento, bateria, inversor e cabos.

No montador híbrido, a Loadboard é o rodapé vivo da grid de equipamentos. Ela
mostra totais gerais e subtotais por tensão, preservando a distinção entre
potência contínua, corrente nominal e pico de partida.

## Navigation

Usar o rail administrativo existente, com estes grupos:

- **Operação:** Visão geral, Leads, Propostas.
- **Dimensionamento:** Dimensionamento solar, Cargas, Sistema híbrido,
  Inversor e cabos.
- **Catálogos:** Materiais solares, Catálogo de cargas, Revisão de fontes.

O rail pode recolher para ícones em desktop. Em mobile vira um drawer acionado
por botão com `aria-expanded`.

## Iconography

Usar uma família única de ícones lineares existente no projeto ou SVGs
consistentes de 1.5px. Ícones nunca substituem texto em ações críticas.
Não usar emojis ou caracteres decorativos como ícones de navegação.

## Motion

- Arrastar: item acompanha o ponteiro com escala `1.015` e sombra discreta.
- Soltar: faixa de destino recebe destaque de `120ms`.
- Recalcular: Loadboard atualiza em `180ms`, sem deslocar o layout.
- Drawer: `160ms`, ease-out.
- Sem bounce, elasticidade ou animações infinitas.
- `prefers-reduced-motion: reduce` elimina transformações e reduz transições a
  troca instantânea de estado.

## Accessibility Contract

- WCAG 2.2 AA.
- Contraste mínimo de 4.5:1 para texto e 3:1 para texto grande e componentes.
- Foco visível em todos os controles.
- Drag-and-drop sempre possui alternativa por teclado: selecionar, mover com
  setas e confirmar com Enter.
- Toda alteração de cálculo anuncia resumo em região `aria-live="polite"`.
- Alerta bloqueante usa `role="alert"`.
- Alvos de toque têm pelo menos 44px.
- Nenhuma informação depende somente de cor.

## Anti-Slop Lock

- Sem gradiente de texto.
- Sem fundo creme/bege genérico em toda a aplicação.
- Sem três cards idênticos repetidos.
- Sem cards aninhados.
- Sem eyebrow minúsculo repetido acima de cada título.
- Sem números falsamente precisos ou slogans vazios.
- Sem visual de “dashboard de IA” com brilho roxo/azul.
