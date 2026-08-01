import { existsSync, readFileSync } from "node:fs";
import { homedir } from "node:os";
import { resolve } from "node:path";

const ApiBase = "https://api.cloudflare.com/client/v4";
const GraphqlUrl = `${ApiBase}/graphql`;

const args = parseArgs(process.argv.slice(2));
loadEnvFiles(args);

const token = firstNonEmpty(
  process.env.CLOUDFLARE_AUDIT_TOKEN,
  process.env.CLOUDFLARE_API_TOKEN,
);

if (!token) {
  fail("Missing CLOUDFLARE_AUDIT_TOKEN or CLOUDFLARE_API_TOKEN.");
}

const lookbackHours = numberFromArgOrEnv(args.hours, "CLOUDFLARE_LOOKBACK_HOURS", 12);
const includeBotScore = boolFromArgOrEnv(args.botScore, "CLOUDFLARE_INCLUDE_BOT_SCORE", false);
const includeNetworkDetail = boolFromArgOrEnv(
  args.networkDetail,
  "CLOUDFLARE_INCLUDE_NETWORK_DETAIL",
  false,
);
const zoneNames = listFromArgOrEnv(args.zones, "CLOUDFLARE_ZONE_NAMES", []);
const zoneIdEntries = entriesFromArgOrEnv(args.zoneIds, "CLOUDFLARE_ZONE_IDS");
const hosts = listFromArgOrEnv(args.hosts, "CLOUDFLARE_HOSTS", defaultHosts(zoneNames, zoneIdEntries));

const now = args.until ? parseDateArg("--until", args.until) : new Date();
const since = args.since
  ? parseDateArg("--since", args.since)
  : new Date(now.getTime() - lookbackHours * 60 * 60 * 1000);

if (since >= now) {
  fail("--since must be earlier than --until.");
}

console.log("Cloudflare traffic/security audit");
console.log(`Window: ${since.toISOString()} to ${now.toISOString()}`);
console.log(`Hosts: ${hosts.join(", ") || "(all hosts in selected zones)"}`);
console.log("");

const zones = await resolveZones(zoneNames, zoneIdEntries);
if (zones.length === 0) {
  fail("No zones resolved. Set CLOUDFLARE_ZONE_NAMES or CLOUDFLARE_ZONE_IDS.");
}

for (const zone of zones) {
  console.log(`== ${zone.name} ==`);
  await printTopRequests(zone);
  await printTopUserAgents(zone);
  await printErrorResponses(zone);
  await printSecurityActions(zone);

  if (args.rules) {
    await printRulesets(zone);
  }

  console.log("");
}

async function printTopRequests(zone) {
  const rows = await queryRequestGroups(zone.id, {
    limit: 20,
    filter: withHostFilter({
      datetime_geq: since.toISOString(),
      datetime_lt: now.toISOString(),
    }),
    dimensions: [
      "clientRequestHTTPHost",
      "clientRequestPath",
      "edgeResponseStatus",
      "cacheStatus",
      "userAgent",
      "clientIP",
      "clientCountryName",
      "securityAction",
      ...optionalNetworkDimensions(),
      ...optionalBotScoreDimension(),
    ],
  });

  printRows("Top requests", rows, (row) => [
    row.count,
    row.dimensions.edgeResponseStatus,
    row.dimensions.cacheStatus,
    row.dimensions.clientRequestHTTPHost,
    row.dimensions.clientRequestPath,
    row.dimensions.clientIP,
    row.dimensions.clientCountryName,
    formatNetworkDetail(row),
    formatBotScore(row),
    row.dimensions.securityAction,
    row.dimensions.userAgent,
  ]);
}

async function printTopUserAgents(zone) {
  const rows = await queryRequestGroups(zone.id, {
    limit: 20,
    filter: withHostFilter({
      datetime_geq: since.toISOString(),
      datetime_lt: now.toISOString(),
    }),
    dimensions: [
      "clientRequestHTTPHost",
      "userAgent",
      "clientCountryName",
      "securityAction",
      ...optionalNetworkDimensions(),
      ...optionalBotScoreDimension(),
    ],
  });

  printRows("Top user agents", rows, (row) => [
    row.count,
    row.dimensions.clientRequestHTTPHost,
    row.dimensions.clientCountryName,
    formatNetworkDetail(row),
    formatBotScore(row),
    row.dimensions.securityAction,
    row.dimensions.userAgent,
  ]);
}

async function printErrorResponses(zone) {
  const rows = await queryRequestGroups(zone.id, {
    limit: 20,
    filter: withHostFilter({
      datetime_geq: since.toISOString(),
      datetime_lt: now.toISOString(),
      edgeResponseStatus_geq: 400,
    }),
    dimensions: [
      "clientRequestHTTPHost",
      "clientRequestPath",
      "edgeResponseStatus",
      "cacheStatus",
      "userAgent",
      "clientIP",
      "clientCountryName",
      "securityAction",
      ...optionalNetworkDimensions(),
      ...optionalBotScoreDimension(),
    ],
  });

  printRows("4xx/5xx responses", rows, (row) => [
    row.count,
    row.dimensions.edgeResponseStatus,
    row.dimensions.cacheStatus,
    row.dimensions.clientRequestHTTPHost,
    row.dimensions.clientRequestPath,
    row.dimensions.clientIP,
    row.dimensions.clientCountryName,
    formatNetworkDetail(row),
    formatBotScore(row),
    row.dimensions.securityAction,
    row.dimensions.userAgent,
  ]);
}

async function printSecurityActions(zone) {
  const rows = await queryRequestGroups(zone.id, {
    limit: 30,
    filter: withHostFilter({
      datetime_geq: since.toISOString(),
      datetime_lt: now.toISOString(),
      securityAction_neq: "",
    }),
    dimensions: [
      "securityAction",
      "securitySource",
      "clientRequestHTTPHost",
      "clientRequestPath",
      "edgeResponseStatus",
      "cacheStatus",
      "userAgent",
      "clientIP",
      "clientCountryName",
      ...optionalNetworkDimensions(),
      ...optionalBotScoreDimension(),
    ],
  });

  printRows("Security actions", rows, (row) => [
    row.count,
    row.dimensions.securityAction,
    row.dimensions.securitySource,
    row.dimensions.edgeResponseStatus,
    row.dimensions.cacheStatus,
    row.dimensions.clientRequestHTTPHost,
    row.dimensions.clientRequestPath,
    row.dimensions.clientIP,
    row.dimensions.clientCountryName,
    formatNetworkDetail(row),
    formatBotScore(row),
    row.dimensions.userAgent,
  ]);
}

async function printRulesets(zone) {
  try {
    const data = await cloudflareRest(`${ApiBase}/zones/${zone.id}/rulesets`);
    const rulesets = data.result ?? [];

    console.log("-- Rulesets --");
    if (rulesets.length === 0) {
      console.log("No rulesets.");
      return;
    }

    for (const ruleset of rulesets) {
      console.log(
        [
          ruleset.phase,
          ruleset.kind,
          ruleset.name,
          ruleset.id,
          `rules=${ruleset.rules?.length ?? "n/a"}`,
        ].map(formatCell).join(" | "),
      );
    }
  } catch (error) {
    console.log("-- Rulesets --");
    console.log(`Could not read rulesets: ${error.message}`);
  }
}

async function queryRequestGroups(zoneTag, options) {
  const dimensions = options.dimensions.join("\n");
  const query = `
    query TrafficGroups($zoneTag: string!, $filter: ZoneHttpRequestsAdaptiveGroupsFilter_InputObject!, $limit: uint64!) {
      viewer {
        zones(filter: { zoneTag: $zoneTag }) {
          httpRequestsAdaptiveGroups(limit: $limit, filter: $filter, orderBy: [count_DESC]) {
            count
            dimensions {
              ${dimensions}
            }
            sum {
              edgeResponseBytes
              visits
            }
          }
        }
      }
    }
  `;

  const data = await cloudflareGraphql(query, {
    zoneTag,
    filter: options.filter,
    limit: options.limit,
  });

  return data.viewer.zones[0]?.httpRequestsAdaptiveGroups ?? [];
}

async function resolveZones(names, idEntries) {
  const zones = [...idEntries.map(([name, id]) => ({ id, name }))];
  const knownNames = new Set(zones.map((zone) => zone.name));

  for (const name of names) {
    if (knownNames.has(name)) {
      continue;
    }

    const url = new URL(`${ApiBase}/zones`);
    url.searchParams.set("name", name);
    url.searchParams.set("per_page", "1");

    const data = await cloudflareRest(url.toString());
    const zone = data.result?.[0];
    if (!zone) {
      console.warn(`Could not resolve Cloudflare zone: ${name}`);
      continue;
    }

    zones.push({ id: zone.id, name: zone.name });
  }

  return zones;
}

async function cloudflareRest(url) {
  const response = await fetch(url, {
    headers: { Authorization: `Bearer ${token}` },
  });
  const data = await response.json();

  if (!response.ok || data.success === false) {
    throw new Error(`Cloudflare REST request failed: ${formatCloudflareErrors(data)}`);
  }

  return data;
}

async function cloudflareGraphql(query, variables) {
  const response = await fetch(GraphqlUrl, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ query, variables }),
  });
  const data = await response.json();

  if (!response.ok || data.errors?.length) {
    throw new Error(`Cloudflare GraphQL request failed: ${formatCloudflareErrors(data)}`);
  }

  return data.data;
}

function loadEnvFiles(parsedArgs) {
  const paths = [];

  if (parsedArgs.envFile) {
    paths.push(resolve(process.cwd(), parsedArgs.envFile));
  }

  if (parsedArgs.profile) {
    paths.push(resolve(homedir(), ".cloudflare-audit", `${parsedArgs.profile}.env`));
  }

  if (paths.length > 0) {
    for (const path of paths) {
      if (existsSync(path)) {
        loadLocalEnv(path);
      }
    }

    return;
  }

  paths.push(resolve(process.cwd(), ".env.cloudflare.local"));
  paths.push(resolve(process.cwd(), "marketing", ".env.cloudflare.local"));

  for (const path of paths) {
    if (existsSync(path)) {
      loadLocalEnv(path);
    }
  }
}

function loadLocalEnv(path) {
  const text = readFileSync(path, "utf8");

  for (const line of text.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) {
      continue;
    }

    const separator = trimmed.indexOf("=");
    if (separator === -1) {
      continue;
    }

    const key = trimmed.slice(0, separator).trim();
    const value = unquote(trimmed.slice(separator + 1).trim());
    process.env[key] ??= value;
  }
}

function unquote(value) {
  if (
    (value.startsWith('"') && value.endsWith('"')) ||
    (value.startsWith("'") && value.endsWith("'"))
  ) {
    return value.slice(1, -1);
  }

  return value;
}

function withHostFilter(filter) {
  if (hosts.length === 0) {
    return filter;
  }

  return { ...filter, clientRequestHTTPHost_in: hosts };
}

function defaultHosts(names, idEntries) {
  const zoneNames = names.length > 0 ? names : idEntries.map(([name]) => name);
  return zoneNames.flatMap((name) => [name, `www.${name}`, `docs.${name}`]);
}

function numberFromEnv(name, fallback) {
  const raw = process.env[name]?.trim();
  if (!raw) {
    return fallback;
  }

  const value = Number(raw);
  if (!Number.isFinite(value) || value <= 0) {
    fail(`${name} must be a positive number.`);
  }

  return value;
}

function numberFromArgOrEnv(argValue, envName, fallback) {
  if (argValue !== undefined) {
    return parsePositiveNumber(`--${flagName(envName)}`, argValue);
  }

  return numberFromEnv(envName, fallback);
}

function listFromEnv(name, fallback) {
  const raw = process.env[name]?.trim();
  if (!raw) {
    return fallback;
  }

  return splitList(raw);
}

function listFromArgOrEnv(argValue, envName, fallback) {
  if (argValue !== undefined) {
    return splitList(argValue);
  }

  return listFromEnv(envName, fallback);
}

function entriesFromArgOrEnv(argValue, envName) {
  return listFromArgOrEnv(argValue, envName, []).map((entry) => {
    const separator = entry.indexOf("=");
    if (separator === -1) {
      fail(`${envName} entries must be in name=zone-id format.`);
    }

    const name = entry.slice(0, separator).trim();
    const id = entry.slice(separator + 1).trim();
    if (!name || !id) {
      fail(`${envName} entries must include both a name and zone id.`);
    }

    return [name, id];
  });
}

function boolFromEnv(name, fallback) {
  const raw = process.env[name]?.trim().toLowerCase();
  if (!raw) {
    return fallback;
  }

  return ["1", "true", "yes", "on"].includes(raw);
}

function boolFromArgOrEnv(argValue, envName, fallback) {
  if (argValue !== undefined) {
    return argValue;
  }

  return boolFromEnv(envName, fallback);
}

function splitList(raw) {
  return raw
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

function parseArgs(rawArgs) {
  const parsed = {};

  for (let index = 0; index < rawArgs.length; index += 1) {
    const arg = rawArgs[index];

    if (arg === "--help" || arg === "-h") {
      printHelp();
      process.exit(0);
    }

    if (arg === "--profile") {
      parsed.profile = requireValue(rawArgs, (index += 1), arg);
      continue;
    }

    if (arg === "--env-file") {
      parsed.envFile = requireValue(rawArgs, (index += 1), arg);
      continue;
    }

    if (arg === "--hours") {
      parsed.hours = requireValue(rawArgs, (index += 1), arg);
      continue;
    }

    if (arg === "--since") {
      parsed.since = requireValue(rawArgs, (index += 1), arg);
      continue;
    }

    if (arg === "--until") {
      parsed.until = requireValue(rawArgs, (index += 1), arg);
      continue;
    }

    if (arg === "--zones") {
      parsed.zones = requireValue(rawArgs, (index += 1), arg);
      continue;
    }

    if (arg === "--zone-ids") {
      parsed.zoneIds = requireValue(rawArgs, (index += 1), arg);
      continue;
    }

    if (arg === "--hosts") {
      parsed.hosts = requireValue(rawArgs, (index += 1), arg);
      continue;
    }

    if (arg === "--bot-score") {
      parsed.botScore = true;
      continue;
    }

    if (arg === "--network-detail") {
      parsed.networkDetail = true;
      continue;
    }

    if (arg === "--rules") {
      parsed.rules = true;
      continue;
    }

    fail(`Unknown argument: ${arg}. Run with --help for usage.`);
  }

  return parsed;
}

function requireValue(rawArgs, index, flag) {
  const value = rawArgs[index];
  if (!value || value.startsWith("--")) {
    fail(`${flag} requires a value.`);
  }

  return value;
}

function parsePositiveNumber(label, raw) {
  const value = Number(raw);
  if (!Number.isFinite(value) || value <= 0) {
    fail(`${label} must be a positive number.`);
  }

  return value;
}

function parseDateArg(label, raw) {
  const date = new Date(raw);
  if (Number.isNaN(date.getTime())) {
    fail(`${label} must be a valid date/time, for example 2026-06-01T00:00:00Z.`);
  }

  return date;
}

function printHelp() {
  console.log(`Usage: bun tools/cloudflare-audit/cloudflare-audit.mjs [options]

Options:
  --profile <name>       Load %USERPROFILE%\\.cloudflare-audit\\<name>.env.
  --env-file <path>      Load a specific dotenv-style file.
  --hours <number>       Relative lookback window ending now.
  --since <datetime>     Absolute start time, for example 2026-06-01T00:00:00Z.
  --until <datetime>     Absolute end time. Defaults to now.
  --zones <list>         Comma-separated zone names.
  --zone-ids <list>      Comma-separated name=zone-id pairs.
  --hosts <list>         Comma-separated hosts to include.
  --bot-score            Include botScore dimensions if supported.
  --network-detail       Include ASN detail dimensions if supported.
  --rules                Also list zone rulesets.
  --help                 Show this help.

Examples:
  bun tools/cloudflare-audit/cloudflare-audit.mjs --profile s1 --hours 6
  bun tools/cloudflare-audit/cloudflare-audit.mjs --profile mlvscan --hours 24 --rules`);
}

function optionalBotScoreDimension() {
  return includeBotScore ? ["botScore"] : [];
}

function optionalNetworkDimensions() {
  return includeNetworkDetail ? ["clientAsn", "clientASNDescription"] : [];
}

function formatNetworkDetail(row) {
  if (!includeNetworkDetail) {
    return "asn=n/a";
  }

  return `${row.dimensions.clientAsn || "-"} ${row.dimensions.clientASNDescription || "-"}`;
}

function formatBotScore(row) {
  return includeBotScore ? `bot=${row.dimensions.botScore}` : "bot=n/a";
}

function printRows(title, rows, mapRow) {
  console.log(`-- ${title} --`);
  if (rows.length === 0) {
    console.log("No rows.");
    return;
  }

  for (const row of rows) {
    console.log(mapRow(row).map(formatCell).join(" | "));
  }
}

function formatCell(value) {
  if (value === null || value === undefined || value === "") {
    return "-";
  }

  return String(value).replace(/\s+/g, " ").slice(0, 180);
}

function formatCloudflareErrors(data) {
  const errors = data.errors ?? data.messages ?? [];
  if (errors.length === 0) {
    return JSON.stringify(data);
  }

  return errors.map(formatCloudflareError).join("; ");
}

function formatCloudflareError(error) {
  const message = error.message ?? JSON.stringify(error);

  if (message.includes("Cannot use the access token from location")) {
    return `${message}. The token has Client IP Address Filtering; add this public IP to the token or create an audit token without IP filtering.`;
  }

  if (message.includes("com.cloudflare.api.account.zone.analytics.read")) {
    return `${message}. Add Account / Account Analytics / Read and include the affected zone in Zone resources.`;
  }

  if (message.includes("Authentication error")) {
    return `${message}. Check that the token is scoped to the specific zone and has the required read permissions.`;
  }

  return message;
}

function firstNonEmpty(...values) {
  return values.find((value) => value?.trim())?.trim();
}

function flagName(envName) {
  return envName.toLowerCase().replace(/^cloudflare_/, "").replaceAll("_", "-");
}

function fail(message) {
  console.error(message);
  process.exit(1);
}
