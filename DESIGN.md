# Design System

## Direction

Energia limpa com precisão de engenharia. A composição combina fundo azul-marinho profundo, superfícies neutras claras, amarelo solar como acento funcional e verde da marca usado com parcimônia. A sensação deve ser premium e segura, não temática ou ecológica demais.

## Palette

- Ink: `oklch(0.22 0.04 255)`
- Ink strong: `oklch(0.14 0.035 255)`
- Surface: `oklch(0.98 0.01 95)`
- Surface muted: `oklch(0.94 0.018 95)`
- Solar: `oklch(0.80 0.17 85)`
- Solar strong: `oklch(0.68 0.16 78)`
- Brand green: `oklch(0.54 0.16 157)`
- Body text: `oklch(0.35 0.035 255)`
- Muted text: `oklch(0.52 0.025 255)`

## Typography

Use `Manrope` for headings and interface text, with strong weight contrast and fluid sizing. Use `Source Sans 3` for long-form reading where a more open texture improves scanning. Avoid all-caps body copy and keep prose between 65 and 75 characters per line.

## Layout

Use generous section rhythm, a 12-column desktop grid and fluid mobile stacking. The hero is an asymmetric split: proof and CTA on the left, a real project image and compact technical callout on the right. Use cards only for distinct actions or evidence; prefer open sections, dividers and image-led compositions elsewhere.

## Components

- Header with logo, concise navigation and primary WhatsApp CTA.
- Hero with one clear claim, one primary CTA and one low-friction secondary action.
- Proof strip for installed capacity, projects and service area.
- Service selector with four audiences.
- Process timeline for diagnosis, sizing, proposal and installation.
- Project gallery with real local assets.
- Lead form with visible privacy consent and inline validation.
- Admin surfaces follow the same tokens but use denser spacing.

## Motion

Use restrained reveal motion for hero media and section transitions. Never hide essential content until animation runs. Respect `prefers-reduced-motion: reduce` by disabling transforms and duration-based transitions.
