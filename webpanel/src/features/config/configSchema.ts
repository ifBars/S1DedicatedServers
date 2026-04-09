import type { ConfigSnapshot } from "@/lib/panel-api"

export type ConfigSectionId = keyof ConfigSnapshot

export type ConfigFieldKind = "text" | "number" | "password" | "boolean" | "textarea"

export type ConfigFieldDefinition = {
  key: string
  label: string
  description: string
  kind: ConfigFieldKind
  placeholder?: string
  readOnly?: boolean
  min?: number
  step?: number
  trueLabel?: string
  falseLabel?: string
}

export type ConfigSectionDefinition = {
  id: ConfigSectionId
  label: string
  description: string
  fields: ConfigFieldDefinition[]
}

export const CONFIG_SECTIONS: ConfigSectionDefinition[] = [
  {
    id: "server",
    label: "Server",
    description: "Name, description, capacity, and inbound game access.",
    fields: [
      {
        key: "serverName",
        label: "Server name",
        description: "Primary label shown to operators and connected players.",
        kind: "text",
      },
      {
        key: "serverDescription",
        label: "Server description",
        description: "Short description visible in the panel header.",
        kind: "textarea",
      },
      {
        key: "maxPlayers",
        label: "Player slots",
        description: "Maximum number of connected players allowed.",
        kind: "number",
        min: 1,
      },
      {
        key: "serverPort",
        label: "Game port",
        description: "Port exposed for gameplay traffic.",
        kind: "number",
        min: 1,
      },
      {
        key: "serverPassword",
        label: "Server password",
        description: "Leave empty when the server should be joinable without a password.",
        kind: "password",
      },
    ],
  },
  {
    id: "authentication",
    label: "Authentication",
    description: "Steam identity, mod verification, and trust enforcement.",
    fields: [
      {
        key: "authProvider",
        label: "Authentication provider",
        description: "Selected provider name used for player identity checks.",
        kind: "text",
      },
      {
        key: "authTimeoutSeconds",
        label: "Auth timeout (seconds)",
        description: "Seconds before a pending authentication flow fails.",
        kind: "number",
        min: 1,
      },
      {
        key: "authAllowLoopbackBypass",
        label: "Loopback bypass",
        description: "Allow the local loopback connection to bypass auth.",
        kind: "boolean",
        trueLabel: "Allowed",
        falseLabel: "Disabled",
      },
      {
        key: "modVerificationEnabled",
        label: "Mod verification",
        description: "Require mod verification before sessions are trusted.",
        kind: "boolean",
        trueLabel: "Enabled",
        falseLabel: "Disabled",
      },
      {
        key: "modVerificationTimeoutSeconds",
        label: "Verification timeout (seconds)",
        description: "Seconds allowed for client mod verification.",
        kind: "number",
        min: 1,
      },
      {
        key: "strictClientModMode",
        label: "Strict client mod mode",
        description: "Enforce a stricter interpretation of client mod mismatches.",
        kind: "boolean",
        trueLabel: "Strict",
        falseLabel: "Flexible",
      },
      {
        key: "blockKnownRiskyClientMods",
        label: "Block risky mods",
        description: "Reject clients that expose known risky mod signatures.",
        kind: "boolean",
        trueLabel: "Blocking",
        falseLabel: "Passive",
      },
      {
        key: "allowUnpairedClientMods",
        label: "Allow unpaired client mods",
        description: "Permit client-only mods without server-side equivalents.",
        kind: "boolean",
        trueLabel: "Allowed",
        falseLabel: "Blocked",
      },
      {
        key: "steamGameServerLogOnAnonymous",
        label: "Anonymous Steam logon",
        description: "Use anonymous Steam game server credentials when possible.",
        kind: "boolean",
        trueLabel: "Anonymous",
        falseLabel: "Manual",
      },
      {
        key: "steamGameServerToken",
        label: "Steam game server token",
        description: "Dedicated server token for authenticated Steam logon.",
        kind: "password",
      },
      {
        key: "steamGameServerQueryPort",
        label: "Steam query port",
        description: "Port advertised to Steam server queries.",
        kind: "number",
        min: 1,
      },
      {
        key: "steamGameServerMode",
        label: "Steam visibility mode",
        description: "Steam server advertising mode label.",
        kind: "text",
      },
      {
        key: "steamWebApiIdentity",
        label: "Steam Web API identity",
        description: "Application identity used with the Steam Web API path.",
        kind: "text",
      },
      {
        key: "steamWebApiKey",
        label: "Steam Web API key",
        description: "Required when the Steam Web API provider is active.",
        kind: "password",
      },
    ],
  },
  {
    id: "tcpConsole",
    label: "TCP Console",
    description: "Telnet-style remote console access for trusted operators.",
    fields: [
      {
        key: "tcpConsoleEnabled",
        label: "TCP console",
        description: "Enable the remote console transport alongside stdio.",
        kind: "boolean",
        trueLabel: "Enabled",
        falseLabel: "Disabled",
      },
      {
        key: "tcpConsoleBindAddress",
        label: "Bind address",
        description: "Prefer loopback unless deliberately exposed.",
        kind: "text",
      },
      {
        key: "tcpConsolePort",
        label: "TCP port",
        description: "Port used by the telnet-compatible console server.",
        kind: "number",
        min: 1,
      },
      {
        key: "tcpConsoleMaxConnections",
        label: "Max connections",
        description: "Maximum concurrent operator sessions.",
        kind: "number",
        min: 1,
      },
      {
        key: "tcpConsoleRequirePassword",
        label: "Require password",
        description: "Demand a password before remote console sessions authenticate.",
        kind: "boolean",
        trueLabel: "Required",
        falseLabel: "Open",
      },
      {
        key: "tcpConsolePassword",
        label: "Console password",
        description: "Password required when remote console auth is enabled.",
        kind: "password",
      },
      {
        key: "stdioConsoleMode",
        label: "Stdio console mode",
        description: "Current interaction mode for the local host console.",
        kind: "text",
        readOnly: true,
      },
    ],
  },
  {
    id: "webPanel",
    label: "Web Panel",
    description: "Embedded localhost UI, launch behavior, and session exposure.",
    fields: [
      {
        key: "webPanelEnabled",
        label: "Integrated panel",
        description: "Enable the localhost web control panel.",
        kind: "boolean",
        trueLabel: "Enabled",
        falseLabel: "Disabled",
      },
      {
        key: "webPanelBindAddress",
        label: "Bind address",
        description: "V1 is intended for loopback-only access.",
        kind: "text",
      },
      {
        key: "webPanelPort",
        label: "Panel port",
        description: "HTTP port used by the embedded panel.",
        kind: "number",
        min: 1,
      },
      {
        key: "webPanelOpenBrowserOnStart",
        label: "Open browser on start",
        description: "Attempt to open the launch URL automatically.",
        kind: "boolean",
        trueLabel: "Enabled",
        falseLabel: "Manual",
      },
      {
        key: "webPanelSessionMinutes",
        label: "Session minutes",
        description: "Lifetime of the localhost browser session.",
        kind: "number",
        min: 1,
      },
      {
        key: "webPanelExposeLogs",
        label: "Expose logs",
        description: "Mirror runtime logs into the browser panel.",
        kind: "boolean",
        trueLabel: "Enabled",
        falseLabel: "Disabled",
      },
    ],
  },
  {
    id: "gameplay",
    label: "Gameplay",
    description: "Time progression and dedicated-server gameplay rules.",
    fields: [
      {
        key: "ignoreGhostHostForSleep",
        label: "Ignore ghost host for sleep",
        description: "Exclude the loopback host from multiplayer sleep checks.",
        kind: "boolean",
        trueLabel: "Ignored",
        falseLabel: "Counted",
      },
      {
        key: "timeProgressionMultiplier",
        label: "Time multiplier",
        description: "Scale the in-game time progression rate.",
        kind: "number",
        min: 0,
        step: 0.1,
      },
      {
        key: "allowSleeping",
        label: "Allow sleeping",
        description: "Permit players to advance time through sleep.",
        kind: "boolean",
        trueLabel: "Allowed",
        falseLabel: "Blocked",
      },
      {
        key: "pauseGameWhenEmpty",
        label: "Pause when empty",
        description: "Pause simulation when no non-loopback players are connected.",
        kind: "boolean",
        trueLabel: "Enabled",
        falseLabel: "Disabled",
      },
    ],
  },
  {
    id: "autosave",
    label: "Autosave",
    description: "Periodic saves and save triggers tied to player movement.",
    fields: [
      {
        key: "autoSaveEnabled",
        label: "Autosave loop",
        description: "Run scheduled saves in the background.",
        kind: "boolean",
        trueLabel: "Enabled",
        falseLabel: "Manual only",
      },
      {
        key: "autoSaveIntervalMinutes",
        label: "Autosave interval (minutes)",
        description: "Minutes between scheduled saves.",
        kind: "number",
        min: 1,
      },
      {
        key: "autoSaveOnPlayerJoin",
        label: "Save on join",
        description: "Trigger a save when a player connects.",
        kind: "boolean",
        trueLabel: "Enabled",
        falseLabel: "Disabled",
      },
      {
        key: "autoSaveOnPlayerLeave",
        label: "Save on leave",
        description: "Trigger a save when a player disconnects.",
        kind: "boolean",
        trueLabel: "Enabled",
        falseLabel: "Disabled",
      },
    ],
  },
  {
    id: "logging",
    label: "Logging",
    description: "Diagnostic streams and subsystem-specific runtime instrumentation.",
    fields: [
      { key: "debugMode", label: "Debug mode", description: "Enable broad debug logging mode.", kind: "boolean" },
      { key: "verboseLogging", label: "Verbose logging", description: "Emit additional diagnostic detail.", kind: "boolean", trueLabel: "Verbose", falseLabel: "Compact" },
      { key: "logPlayerActions", label: "Player actions", description: "Audit player-driven actions.", kind: "boolean" },
      { key: "logAdminCommands", label: "Admin commands", description: "Record privileged command activity.", kind: "boolean" },
      { key: "enablePerformanceMonitoring", label: "Performance monitoring", description: "Emit performance instrumentation.", kind: "boolean" },
      { key: "logNetworkingDebug", label: "Networking debug", description: "Trace networking internals.", kind: "boolean" },
      { key: "logMessageRoutingDebug", label: "Message routing", description: "Trace custom message routing.", kind: "boolean" },
      { key: "logMessagingBackendDebug", label: "Messaging backend", description: "Trace messaging backend events.", kind: "boolean" },
      { key: "logStartupDebug", label: "Startup debug", description: "Emit startup diagnostics.", kind: "boolean" },
      { key: "logServerNetworkDebug", label: "Server network debug", description: "Trace server network lifecycle.", kind: "boolean" },
      { key: "logPlayerLifecycleDebug", label: "Player lifecycle", description: "Trace connect/auth/spawn phases.", kind: "boolean" },
      { key: "logAuthenticationDebug", label: "Authentication debug", description: "Trace authentication decisions.", kind: "boolean" },
    ],
  },
  {
    id: "performance",
    label: "Performance",
    description: "Frame pacing and dedicated runtime performance targets.",
    fields: [
      {
        key: "targetFrameRate",
        label: "Target frame rate",
        description: "Frame pacing target for the server runtime.",
        kind: "number",
        min: 1,
      },
      {
        key: "vSyncCount",
        label: "VSync count",
        description: "Vertical sync setting used by the runtime host.",
        kind: "number",
        min: 0,
      },
    ],
  },
  {
    id: "storage",
    label: "Storage",
    description: "Save path selection and the currently resolved storage target.",
    fields: [
      {
        key: "saveGamePath",
        label: "Custom save path",
        description: "Leave empty to use the default dedicated-server storage path.",
        kind: "text",
      },
      {
        key: "resolvedSaveGamePath",
        label: "Resolved save path",
        description: "Current effective save path used by the server.",
        kind: "textarea",
        readOnly: true,
      },
    ],
  },
]

export function getConfigSection(sectionId: ConfigSectionId) {
  return CONFIG_SECTIONS.find((section) => section.id === sectionId) ?? CONFIG_SECTIONS[0]
}
