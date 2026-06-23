const DISCOVERY_LINKS = [
  '</.well-known/api-catalog>; rel="api-catalog"; type="application/linkset+json"',
  '</openapi.json>; rel="service-desc"; type="application/openapi+json"',
  '</llms.txt>; rel="service-doc"; type="text/markdown"',
  '</.well-known/agent-skills/index.json>; rel="agent-skills"; type="application/json"',
  '</.well-known/mcp/server-card.json>; rel="mcp-server-card"; type="application/json"',
  '</.well-known/agent-card.json>; rel="describedby"; type="application/json"',
];

const MARKDOWN_HOME = `# S1DedicatedServers

S1DedicatedServers is a free, open-source dedicated server mod and hosting guide surface for Schedule I.

## Primary actions

- Download the latest server and client release: https://github.com/ifBars/S1DedicatedServers/releases
- Read the setup documentation: https://docs.s1servers.com/
- Generate a server configuration: https://s1servers.com/config-generator
- Review hosting provider guidance: https://docs.s1servers.com/docs/hosting-providers.html

## What agents can help with

- Find the correct download and documentation links for a Schedule I dedicated server setup.
- Explain the basic install flow: download, create a server install, generate configuration, set saveGamePath, launch, and connect.
- Direct users to the config generator when they need a valid server_config.toml.
- Use the API catalog, OpenAPI description, MCP server card, and agent skills index exposed from this site for discovery.
`;

function withDiscoveryHeaders(response: Response): Response {
  const headers = new Headers(response.headers);
  for (const link of DISCOVERY_LINKS) {
    headers.append("Link", link);
  }
  headers.append("Vary", "Accept");
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers,
  });
}

export async function onRequest(context: { request: Request; next: () => Promise<Response> }) {
  const url = new URL(context.request.url);
  const accept = context.request.headers.get("Accept") ?? "";

  if (url.pathname === "/" && accept.toLowerCase().includes("text/markdown")) {
    return withDiscoveryHeaders(
      new Response(MARKDOWN_HOME, {
        headers: {
          "Content-Type": "text/markdown; charset=utf-8",
          "x-markdown-tokens": String(Math.ceil(MARKDOWN_HOME.length / 4)),
        },
      }),
    );
  }

  return withDiscoveryHeaders(await context.next());
}
