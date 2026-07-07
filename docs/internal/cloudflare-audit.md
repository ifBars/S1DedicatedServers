# Cloudflare Traffic And Security Audit

This guide is for auditing marketing-site traffic without relying on an
interactive Cloudflare dashboard session.

## Current Wrangler State

`wrangler` is not installed on PATH in this workspace, but it can run through
Bun:

```powershell
bunx wrangler --version
bunx wrangler whoami
```

The current machine has a Wrangler OAuth login. That is useful for manual
Workers/Pages work, but it is not the right long-term credential for repeatable
traffic audits because API scripts and future agents need explicit,
least-privilege API tokens.

## Token To Create

Create one read-only token for audits first. Use a separate edit token only if
an agent should actually change WAF, cache, DNS, or Pages settings.

### Read-Only Audit Token

Name:

```text
codex-cloudflare-audit-read
```

Permissions:

```text
Account / Account Analytics / Read
Account / Account Rulesets / Read
Account / Cloudflare Pages / Read
Account / Logs / Read

Zone / Zone / Read
Zone / Analytics / Read
Zone / Zone Settings / Read
Zone / DNS / Read
Zone / Zone WAF / Read
Zone / Firewall Services / Read
Zone / Page Rules / Read
Zone / Cache Settings / Read
Zone / Bot Management / Read
```

If Cloudflare does not show a permission for the zone's current plan, skip that
permission and run the audit. The helper reports the missing permission if a
query needs it.

Resources:

```text
Account resources: Include -> your Cloudflare account
Zone resources: Include -> Specific zone -> s1servers.com
Zone resources: Include -> Specific zone -> s1ds.com
```

Add other project zones, such as MLVScan, to the same read-only token if you want
one shared audit token. If you want tighter isolation, create one audit token per
project with the same permissions and fewer zones.

Client IP Address Filtering:

Leave this empty for local agents unless you are prepared to update it whenever
your public IP changes. A token restricted to the wrong IP fails with:

```text
Cannot use the access token from location: <ip>
```

### Optional Edit Token

Only create this when an agent should make Cloudflare changes after review.

Name:

```text
codex-cloudflare-security-edit-s1
```

Permissions:

```text
Account / Account Rulesets / Edit

Zone / Zone / Read
Zone / Zone Settings / Edit
Zone / DNS / Edit
Zone / Zone WAF / Edit
Zone / Page Rules / Edit
Zone / Cache Settings / Edit
```

Resources:

```text
Zone resources: Include -> Specific zone -> s1servers.com
Zone resources: Include -> Specific zone -> s1ds.com
```

Do not use the edit token for normal audits. Store it separately and require a
human confirmation before applying rules.

## Local Profile Storage

Store secrets outside git in named profiles:

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\.cloudflare-audit" | Out-Null
Copy-Item .\tools\cloudflare-audit\profiles\s1.example.env "$env:USERPROFILE\.cloudflare-audit\s1.env"
notepad "$env:USERPROFILE\.cloudflare-audit\s1.env"
```

Example `s1.env`:

```dotenv
CLOUDFLARE_AUDIT_TOKEN=
CLOUDFLARE_ZONE_NAMES=s1servers.com,s1ds.com
CLOUDFLARE_HOSTS=s1servers.com,www.s1servers.com,docs.s1servers.com,s1ds.com,www.s1ds.com,docs.s1ds.com
CLOUDFLARE_LOOKBACK_HOURS=12
CLOUDFLARE_INCLUDE_BOT_SCORE=false
CLOUDFLARE_INCLUDE_NETWORK_DETAIL=false
```

Example `mlvscan.env`:

```dotenv
CLOUDFLARE_AUDIT_TOKEN=
CLOUDFLARE_ZONE_NAMES=mlvscan.com
CLOUDFLARE_HOSTS=mlvscan.com,www.mlvscan.com,api.mlvscan.com
CLOUDFLARE_LOOKBACK_HOURS=12
CLOUDFLARE_INCLUDE_BOT_SCORE=false
CLOUDFLARE_INCLUDE_NETWORK_DETAIL=false
```

You can create it from the tracked template:

```powershell
Copy-Item .\tools\cloudflare-audit\profiles\mlvscan.example.env "$env:USERPROFILE\.cloudflare-audit\mlvscan.env"
```

If a token cannot list zones, add explicit zone IDs:

```dotenv
CLOUDFLARE_ZONE_IDS=s1servers.com=<zone-id>,s1ds.com=<zone-id>
```

## Running Audits

From this repository:

```powershell
bun tools/cloudflare-audit/cloudflare-audit.mjs --profile s1 --hours 6
bun tools/cloudflare-audit/cloudflare-audit.mjs --profile s1 --hours 24 --rules
```

From another repository, call this helper by absolute path or copy the
`tools/cloudflare-audit` folder into that repo:

```powershell
bun C:\Users\ghost\Desktop\Coding\ScheduleOne\DedicatedServerMod\tools\cloudflare-audit\cloudflare-audit.mjs --profile mlvscan --hours 24
```

Use `--bot-score` only when the zone supports Bot Management fields. Use
`--network-detail` only when ASN detail fields are available on the plan.
