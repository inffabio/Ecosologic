# n8n Supplier Discovery

The production compose file includes n8n as a separate service. It uses the existing PostgreSQL server in the dedicated `n8n` schema and persists encryption data in `n8n-data`.

## Required variables

Add these values to the production environment file without committing it:

```env
N8N_ENCRYPTION_KEY=generate-a-long-random-value
N8N_HOST=n8n.example.com
N8N_PROTOCOL=https
N8N_WEBHOOK_URL=https://n8n.example.com/
```

The n8n instance should be exposed through the existing reverse proxy with HTTPS and its own authentication. Do not expose port `5678` publicly.

## Workflow

Create a workflow with this sequence:

1. Cron or manual trigger.
2. Tavily or Brave Search node, using a query such as `fornecedores de kits solares no Brasil`.
3. OpenAI node to normalize each result into `name`, `website`, `contact`, and `source`.
4. HTTP Request node calling `POST http://api:8080/api/solar-suppliers/import` with the normalized supplier list.
5. Review candidates at `/admin/fornecedores`.

The API stores candidates as `Pending`. The user must complete contact name, phone, and WhatsApp before approval. Approved suppliers are then returned alphabetically by `GET /api/solar-suppliers`.

Keep Tavily/Brave/OpenAI keys in n8n credentials, never in workflow JSON or the repository.
