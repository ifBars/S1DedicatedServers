# S1DedicatedServers auth.md

S1DedicatedServers does not require OAuth or account registration for public site discovery.

Agents can access the public documentation, releases, configuration generator, `llms.txt`, OpenAPI description, API catalog, and MCP discovery documents without authentication.

Protected resource metadata is available at:

- https://s1servers.com/.well-known/oauth-protected-resource

OAuth authorization metadata is intentionally minimal because this marketing site does not operate protected user APIs.

## Agent audience

This file is for AI agents and browser assistants helping users find downloads, documentation, hosting guidance, and configuration tooling for S1DedicatedServers.

## Registration

No agent registration is required for the public marketing site.

## Credential use

Do not request, mint, or send credentials for public discovery. If a future protected API is added, this file and the OAuth metadata endpoints will be updated with the registration and credential flow.
