export type LogEntry = {
  timestampUtc: string
  level: string
  message: string
  source: string
}

export type Overview = {
  serverName: string
  serverDescription: string
  isRunning: boolean
  status: string
  serverPort: number
  currentPlayers: number
  maxPlayers: number
  framesPerSecond: number
  frameTimeMilliseconds: number
  uptimeSeconds: number
  uptimeDisplay: string
  authProvider: string
  autoSaveEnabled: boolean
  autoSaveIntervalMinutes: number
  saveInProgress: boolean
  lastSaveUtc: string | null
  totalGroups: number
  totalUsers: number
  totalBans: number
  totalOperators: number
  totalAdministrators: number
}

export type PlayerRow = {
  clientId: number
  displayName: string
  steamId: string
  trustedUniqueId: string
  isLoopback: boolean
  isConnected: boolean
  isSpawned: boolean
  isAuthenticated: boolean
  isAuthenticationPending: boolean
  isModVerificationComplete: boolean
  isModVerificationPending: boolean
  roleSummary: string
  connectedAtUtc: string
  connectionDuration: string
}

export type ConfigSnapshot = {
  server: {
    serverName: string
    serverDescription: string
    maxPlayers: number
    serverPort: number
    serverPassword: string
  }
  authentication: {
    authProvider: string
    authTimeoutSeconds: number
    authAllowLoopbackBypass: boolean
    modVerificationEnabled: boolean
    modVerificationTimeoutSeconds: number
    blockKnownRiskyClientMods: boolean
    allowUnpairedClientMods: boolean
    strictClientModMode: boolean
    steamGameServerLogOnAnonymous: boolean
    steamGameServerToken: string
    steamGameServerQueryPort: number
    steamGameServerVersion: string
    steamGameServerMode: string
    steamWebApiKey: string
    steamWebApiIdentity: string
  }
  tcpConsole: {
    tcpConsoleEnabled: boolean
    tcpConsoleBindAddress: string
    tcpConsolePort: number
    tcpConsoleMaxConnections: number
    tcpConsoleRequirePassword: boolean
    tcpConsolePassword: string
    stdioConsoleMode: string
  }
  webPanel: {
    webPanelEnabled: boolean
    webPanelBindAddress: string
    webPanelPort: number
    webPanelOpenBrowserOnStart: boolean
    webPanelSessionMinutes: number
    webPanelExposeLogs: boolean
  }
  gameplay: {
    ignoreGhostHostForSleep: boolean
    timeProgressionMultiplier: number
    allowSleeping: boolean
    pauseGameWhenEmpty: boolean
  }
  autosave: {
    autoSaveEnabled: boolean
    autoSaveIntervalMinutes: number
    autoSaveOnPlayerJoin: boolean
    autoSaveOnPlayerLeave: boolean
  }
  logging: {
    debugMode: boolean
    verboseLogging: boolean
    logPlayerActions: boolean
    logAdminCommands: boolean
    enablePerformanceMonitoring: boolean
    logNetworkingDebug: boolean
    logMessageRoutingDebug: boolean
    logMessagingBackendDebug: boolean
    logStartupDebug: boolean
    logServerNetworkDebug: boolean
    logPlayerLifecycleDebug: boolean
    logAuthenticationDebug: boolean
  }
  performance: {
    targetFrameRate: number
    vSyncCount: number
  }
  storage: {
    saveGamePath: string
    resolvedSaveGamePath: string
  }
}

export type BootstrapPayload = {
  version: string
  configFilePath: string
  permissionsFilePath: string
  userDataPath: string
  sessionExpiresAtUtc: string | null
  overview: Overview
  config: ConfigSnapshot
  recentLogs: LogEntry[]
}

export type CommandOutputLine = {
  level: string
  message: string
}

export type CommandResult = {
  succeeded: boolean
  status: string
  commandWord: string
  message: string
  output: CommandOutputLine[]
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    credentials: "same-origin",
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
    ...init,
  })

  if (!response.ok) {
    const message = await response.text()
    throw new Error(message || `${response.status} ${response.statusText}`)
  }

  return (await response.json()) as T
}

export function exchangeToken(token: string) {
  return requestJson<{ authenticated: boolean; expiresAtUtc: string }>(
    "/api/session/exchange-token",
    {
      method: "POST",
      body: JSON.stringify({ token }),
    }
  )
}

export function getBootstrap() {
  return requestJson<BootstrapPayload>("/api/bootstrap")
}

export function getOverview() {
  return requestJson<Overview>("/api/overview")
}

export function getPlayers() {
  return requestJson<PlayerRow[]>("/api/players")
}

export function getConfig() {
  return requestJson<ConfigSnapshot>("/api/config")
}

export function saveConfig(config: ConfigSnapshot) {
  return requestJson<ConfigSnapshot>("/api/config", {
    method: "POST",
    body: JSON.stringify(config),
  })
}

export function triggerSave() {
  return requestJson<{ accepted: boolean }>("/api/actions/save", {
    method: "POST",
    body: "{}",
  })
}

export function reloadConfig() {
  return requestJson<ConfigSnapshot>("/api/actions/reload-config", {
    method: "POST",
    body: "{}",
  })
}

export function requestShutdown() {
  return requestJson<{ accepted: boolean }>("/api/actions/shutdown", {
    method: "POST",
    body: "{}",
  })
}

export function executeCommand(commandLine: string) {
  return requestJson<CommandResult>("/api/console/execute", {
    method: "POST",
    body: JSON.stringify({ commandLine }),
  })
}
