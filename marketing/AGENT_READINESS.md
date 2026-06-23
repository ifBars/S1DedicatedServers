# Agent Readiness Operations

The marketing site is expected to score Level 5 Agent-Native on
https://isitagentready.com/s1servers.com.

## Deployment

Marketing deploys through `.github/workflows/marketing.yml`.

Required GitHub configuration:

- Secret `CLOUDFLARE_ACCOUNT_ID`
- Secret `CLOUDFLARE_API_TOKEN`
- Repository variable or secret `CLOUDFLARE_ZONE_ID`
- Repository variable `CLOUDFLARE_MARKETING_PROJECT_NAME`

`CLOUDFLARE_MARKETING_PROJECT_NAME` must be the Cloudflare Pages project that serves
`https://s1servers.com/`.

`CLOUDFLARE_ZONE_ID` must be the Cloudflare zone id for `s1servers.com`. It can be
stored as either a repository variable or a repository secret. The DNS workflow
falls back to resolving the zone by name, but scoped DNS tokens may not have
permission to list zones.

The Cloudflare API token must be scoped to the `s1servers.com` zone and allow
DNS record management:

- Zone resources: the specific `s1servers.com` zone
- Permissions: `Zone / DNS / Edit`

If the DNSSEC step fails with a permission error, enable DNSSEC manually in
Cloudflare or expand the token to allow DNSSEC changes for the same zone.

## DNS-AID records

The scanner checks DNS-over-HTTPS for DNS-AID records under the `_agents`
namespace. Add these records in Cloudflare DNS for `s1servers.com`.

```dns
_index._agents.s1servers.com. 3600 IN HTTPS 1 s1servers.com. alpn="h2"
_a2a._agents.s1servers.com. 3600 IN HTTPS 1 s1servers.com. alpn="h2"
_mcp._agents.s1servers.com. 3600 IN HTTPS 1 s1servers.com. alpn="h2"
```

If Cloudflare's DNS editor requires a simplified value, use priority `1`, target
`s1servers.com`, and SvcParams `alpn="h2"` for each record.

Enable DNSSEC for the public zone if it is not already enabled. The scanner reports
whether DNSSEC validation returned authenticated data.

## Validation

After deployment and DNS propagation:

```powershell
$response = Invoke-RestMethod `
  -Uri 'https://isitagentready.com/api/scan' `
  -Method Post `
  -ContentType 'application/json' `
  -Body (@{url='https://s1servers.com'} | ConvertTo-Json -Compress)

$response.levelName
$response.checks.discoverability
$response.checks.contentAccessibility
$response.checks.discovery
```

The goal is complete only when `$response.levelName` is `Agent-Native`.
