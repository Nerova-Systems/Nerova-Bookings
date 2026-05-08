---
name: nango-toolbox
description: Universal gateway for any third-party API or SaaS (Google Calendar, Gmail, Slack, Notion, Linear, HubSpot, etc.). TRIGGER on any request to read or modify data in an external product, even when no matching MCP tool is loaded.
---

## When to use

- Any access to an external API.
- External API calls must go through **Nango**.
- Do **not** use MCPs, provider CLIs, or ad-hoc direct API calls outside Nango.

## Configuration (required)

Resolve values in this order.

### Functions project keys

For Nango Functions work, use the local Functions project at `application/nango-integrations/`.

- Store keys in `application/nango-integrations/.env`.
- Use `NANGO_SECRET_KEY_DEV=<dev-secret-key>` for dev.
- Use `NANGO_SECRET_KEY_PROD=<prod-secret-key>` for prod.
- Keep `.env` untracked; `application/nango-integrations/.gitignore` must include `.env`.
- Get keys from Nango Environment Settings > API Keys.

The Nango CLI matches keys by environment name: `NANGO_SECRET_KEY_<ENV_NAME>`.

### Documentation MCP

Use Nango's docs MCP for current function APIs and examples:

```
https://nango.dev/docs/mcp
```

Do not configure Codex's general Nango MCP server as `https://api.nango.dev/mcp` unless the task is explicitly to expose deployed Nango action functions as MCP tools for one specific provider connection.

### Hosted action MCP

Nango's hosted action MCP is connection-scoped. It is only appropriate after a deployed action exists and the caller has a matching `providerConfigKey` and `connectionId`. Do not use it as the default dashboard/API setup for Functions development.

### Backend API calls

For Nango backend API discovery or connection lookup, read the relevant secret from `nango-integrations/.env` for the target environment and send:

- `Authorization: Bearer <NANGO_SECRET_KEY_DEV or NANGO_SECRET_KEY_PROD>`
- `Content-Type: application/json`

Prefer JSON bodies over query params when both exist.

## Discovery (use these first)

List integrations:
```
GET <base-url>/integrations
Authorization: Bearer <secret-key>
```

List connections for an integration:
```
GET <base-url>/connections?integrationId=<unique_key>
Authorization: Bearer <secret-key>
```

List deployed actions/syncs:
```
GET <base-url>/scripts/config
Authorization: Bearer <secret-key>
```

## Choose integration (do this before connection)

- Check if an integration for your API is setup in Nango
- If not, set it up for the user:
  - If the API is OAuth and has Nango provided developer credentials, ask the user to enable it manually in the dashboard.
  - If the API is OAuth and doesn't have Nango developer credentials guide the user to get a client id & secret
  - If the API uses "OAuth client credentials", do not ask the user for client id & secret. Set the integration up and the user will enter them with the Connect Link.
  - In all other cases, set the integration up and proceed with creating a Connect link

Do not ask the user for any Connection specific parameters. Always let it enter these in the Connect link.

## Choose integration + connection (required)

- Ensure the integration (provider) exists in Nango; create it via Nango HTTP API if needed.
- Fetch connections for the integration.
- If multiple connections exist and you cannot disambiguate, ask which `connection_id` to use.

If no connection exists, generate a connect session link:
```
POST <base-url>/connect/sessions
Authorization: Bearer <secret-key>
Content-Type: application/json

{
  "tags": { "end_user_id": "<user-firstname>" },
  "allowed_integrations": ["<unique_key>"]
}
```
Share `data.connect_link` directly with the user (expires in ~30 minutes). Do not send them to the dashboard.

Before provider calls, confirm required scopes on the connection (often `credentials.raw.scope`).

If scopes are insufficient, update scopes at the **integration** level, then ask for permission to delete/recreate the connection:
```
PATCH <base-url>/integrations/<unique_key>
Authorization: Bearer <secret-key>
Content-Type: application/json

{
  "credentials": {
    "type": "OAUTH2",
    "client_id": "<client_id>",
    "client_secret": "<client_secret>",
    "scopes": "scope1,scope2,scope3"
  }
}
```
Scopes are comma-separated (no spaces).

## Access strategy

- Use **Nango proxy** for 1–2 simple provider calls and to explore the API.
- Use a **Nango Action** for multi-call logic, transformations, pagination orchestration, retries/error handling, or reuse.

Prefer reusing an existing deployed action (see `GET <base-url>/scripts/config`).

If you must create a new action, use the `building-nango-function-remotely` skill and deploy before calling it.
If the skill does not exist, offer to install it from https://github.com/NangoHQ/skills

Invoke a deployed action:
```
POST <base-url>/action/trigger
Authorization: Bearer <secret-key>
Provider-Config-Key: <unique_key>
Connection-Id: <connection_id>
Content-Type: application/json

{"action_name": "<action-name>", "input": {...}}
```

## Docs

- https://nango.dev/docs
- https://nango.dev/docs/llms.txt
- https://nango.dev/docs/reference/api
- https://nango.dev/docs/spec.yaml

Nango maintains a docs page for each API it supports (find it in llms.txt). This page contains a link to the external APIs reference.
