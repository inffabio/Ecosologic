# Ecosologic Site + CRM Design

## Goal

Rebuild the Ecosologic institutional website with Angular and Ionic, preserving the existing logo and favicon while adding a small, evolvable CRM. The first operational workflow is on-grid solar sizing and commercial proposal generation. The system must be editable by an administrator without code changes.

## Scope

- Public responsive website for residential, commercial, industrial and agribusiness solar solutions.
- Admin authentication with one initial administrator and an extensible permission model.
- Editable pages, sections, text, images, buttons, contact settings, projects, brands, testimonials and support guides.
- Lead capture from contact and quote forms, with a simple commercial pipeline.
- Rio de Janeiro tariff catalog limited to Light and Enel, mapped to municipalities with manual override.
- Real customer tariff as the preferred input, with ANEEL tariff data as reference and fallback.
- On-grid sizing as the first calculation mode.
- Lei 14.300/2022 and TUSD Fio B parameters stored with effective dates and history.
- Proposal versions generated as PDF from an HTML/CSS template inspired by the supplied InDesign document.
- Growatt MIN 5000 TL-X / ShinePhone support guide with images, videos and editable instructions.

## Explicit Non-Goals For First Release

- Full ERP, inventory, billing or project execution management.
- Automatic engineering approval or final electrical design.
- Off-grid and hybrid sizing beyond extension points and data modeling.
- Automatic scraping of ANEEL data without a reviewed import process.

## Architecture

Use a modular monolith with .NET 9 and Clean Architecture:

- `Domain`: entities, value objects, calculation rules and invariants.
- `Application`: use cases, DTOs, ports and validation.
- `Infrastructure`: EF Core/PostgreSQL, file storage, email and PDF implementation.
- `Api`: REST endpoints, authentication, authorization, Swagger and error handling.
- `Shared`: result/error contracts and cross-cutting primitives.

Initial modules:

- Identity
- Content
- CRM
- Dimensioning
- Tariffs
- Proposals
- Files
- Notifications

Frontend uses Angular + Ionic with public and authenticated admin areas. Content is rendered from typed blocks rather than arbitrary HTML. Uploaded files are stored outside PostgreSQL and referenced by metadata.

## Core Flows

### Lead And Proposal

`Public form -> Lead -> Customer/site data -> Bill inputs -> Dimensioning -> Tariff/Fio B calculation -> Proposal draft -> PDF version -> Send/download -> Pipeline activity`

### Content

`Admin login -> Page editor -> Block ordering/visibility -> Preview -> Publish -> Public site`

### Tariffs

Each tariff stores distributor, municipality coverage, group/subgroup, TUSD, TE, Fio B, unit, source, effective period and audit data. Values entered from the customer's bill override catalog values. ANEEL values are normalized from `R$/MWh` to `R$/kWh` by dividing by 1,000.

The Fio B schedule is configurable and initially seeded with 15% (2023), 30% (2024), 45% (2025), 60% (2026), 75% (2027), and 90% (2028). Definitive post-2028 treatment and acquired-rights rules must remain configurable and be reviewed against current ANEEL/distributor guidance before production use.

### On-Grid Sizing

Inputs include monthly consumption, bill tariff, municipality, distributor, connection type, voltage, HSP/irradiation, losses, module power, inverter power and target generation. Outputs include module quantity, installed kWp, inverter quantity/power, monthly and annual generation, compensated energy, availability cost, estimated bill, savings and assumptions.

The calculation engine will progressively reproduce the supplied workbook, including financial indicators only after the base calculation is validated. Every result stores its inputs, tariff snapshot, rule version and timestamp.

### Proposal PDF

The template includes cover, customer data, system summary, generation chart, savings with Fio B, equipment, investment, payment conditions, warranties, schedule, scope exclusions, assumptions, validity and Ecosologic contact details (`fabio@ecosologic.com.br`, `+55 (21) 99542-4027`). Issued proposals are immutable snapshots; later template edits do not change them.

### Support Guide

The CMS includes a Growatt MIN 5000 TL-X guide centered on the ShinePhone app and the installed datalogger. It covers download, account/plant registration, 2.4 GHz Wi-Fi setup, router/password changes, reset/re-pairing, LED/status checks and escalation to WhatsApp. The datalogger model is selected before showing instructions because the inverter model alone does not define the exact procedure.

## Safety And Reliability

- Incomplete or stale tariff inputs block final proposal issuance and show an explicit warning.
- Estimates are labeled as preliminary and require technical validation.
- Manual distributor changes and all tariff/calculation changes are audited.
- Email failure does not delete a lead or proposal.
- Calculation formulas have unit, boundary and regression tests.
- Authentication, file validation, authorization and rate limiting protect admin and public forms.
- Content preview is separate from published content.

## Delivery Order

1. Repository/app foundation and design tokens.
2. Public website and asset migration.
3. Admin authentication and CMS blocks.
4. Leads and CRM pipeline.
5. Tariff catalog and municipality mapping.
6. On-grid calculation engine.
7. Proposal PDF and versioning.
8. ShinePhone support center.
9. Financial calculation expansion.
10. Off-grid and hybrid modules.

## Acceptance Criteria

- Admin can update a homepage phrase and image without code changes.
- Public contact form creates a CRM lead and sends notification to the configured email.
- A municipality resolves to Light or Enel and can be manually overridden.
- A calculation records the customer's real tariff and the applied Fio B rule version.
- A valid on-grid calculation produces a downloadable proposal PDF.
- Reissuing a proposal preserves its previous version.
- The ShinePhone guide works on mobile and supports editable media links.
- Public site and admin interface are usable on desktop and mobile.
