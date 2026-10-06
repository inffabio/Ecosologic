# Regional Lead Enrichment Design

## Goal

Permitir que uma lista de cidades seja enviada a um webhook autenticado para pesquisar sinais públicos de interesse em energia solar e enriquecer leads existentes no CRM.

## Input

```json
{
  "cities": ["Marica", "Saquarema", "Rio das Ostras", "Macae"],
  "maxLeadsPerCity": 10
}
```

The workflow normalizes city names, removes repeated cities, limits each run to 20 cities and each city to 10 leads.

## Data Flow

1. Authenticated webhook receives the city list.
2. The workflow loads CRM leads that are eligible for enrichment.
3. For each city, Brave searches public indexed pages with solar-interest queries.
4. 9Router extracts only evidence-backed business or public-contact signals.
5. The workflow matches results to existing leads and creates an enrichment note.
6. A CRM activity and follow-up task are created for matched leads.

## Stored Content

The note is marked with `AI_ENRICHMENT` and contains city, intent, potential,
public evidence URL, evidence excerpt and suggested next action. Existing notes
are preserved and the same enrichment block is replaced on reruns.

## Safety

- Brave only searches public indexed content.
- No private profiles, direct messages, login-gated content or automatic outreach.
- Leads without evidence tying them to the requested city are ignored.
- The workflow is rate-limited by city and lead count.

## Success Criteria

- A webhook request with four cities completes successfully.
- Results are restricted to the requested cities.
- Repeating the same request does not append duplicate enrichment blocks.
- Each matched lead receives one activity and one follow-up task per enrichment version.
