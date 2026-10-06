# Internal Secrets Broker

The API exposes OCI Vault values to trusted internal clients such as n8n. The
API is the only container that mounts `/home/hannibal/.oci`; n8n receives only
the requested secret value.

Configure these values in the production environment file, without committing
the file:

```env
OCI_CONFIG_FILE=/app/.oci/config
OCI_PROFILE=DEFAULT
OCI_CONFIG_HOST_PATH=/home/hannibal/.oci
INTERNAL_SECRETS_TOKEN=generate-a-long-random-value
INTERNAL_SECRETS_CACHE_SECONDS=300
OCI_VAULT_OCID=ocid1.vault.oc1...
OCI_SECRET_N8N_API_NAME=N8N-APIKEY
OCI_SECRET_TAVILY_API_NAME=TAVILY-API-KEY
OCI_SECRET_TAVILY_MCP_NAME=TAVILY-MCP
OCI_SECRET_9ROUTER_API_NAME=9ROUTER-API-KEY
OCI_SECRET_BRAVE_API_NAME=BRAVE-API-KEY
```

The private endpoint is:

```text
GET /api/internal/secrets/{name}
X-Internal-Secrets-Token: ${INTERNAL_SECRETS_TOKEN}
```

The logical names map to these OCI secret names:

```text
N8N_API_KEY -> N8N-APIKEY
TAVILY_API_KEY -> TAVILY-API-KEY
TAVILY_MCP -> TAVILY-MCP
9ROUTER_API_KEY -> 9ROUTER-API-KEY
BRAVE_API_KEY -> BRAVE-API-KEY
```

The endpoint returns `401` for a missing or invalid token and `404` for a name
outside the allowlist. Successful values are cached in memory for the
configured TTL and responses are marked `no-store`.

For a user principal in OCI IAM, grant the group used by the mounted
`.oci/config` permission to read secret bundles in the vault. Restrict it to
the Vault OCID whenever possible:

```text
Allow group <oci-group-name> to read secret-bundles in compartment <compartment-name> where target.vault.id = '<vault-ocid>'
```

The Secrets SDK uses the regional Secrets endpoint automatically. A
Cryptographic endpoint is not required for reading secret bundles.
