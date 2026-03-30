import type {
  BootstrapPayload,
  CommandResult,
  ConfigSnapshot,
  LogEntry,
  Overview,
  PlayerRow,
} from "@/lib/panel-api"

export type PanelRuntimeAction = "save" | "reload" | "shutdown" | null

export type PanelRuntimeActions = {
  updateDraftValue: (
    section: keyof ConfigSnapshot,
    key: string,
    value: boolean | number | string
  ) => void
  resetDraft: () => void
  saveWorld: () => Promise<void>
  reloadConfigFromDisk: () => Promise<void>
  shutdown: () => Promise<void>
  saveConfig: () => Promise<void>
  runCommand: (commandLine: string) => Promise<void>
}

export type PanelRuntimeFlags = {
  isBusy: boolean
  isConfigDirty: boolean
  isSavingConfig: boolean
  runningAction: PanelRuntimeAction
  isRunningCommand: boolean
}

export type PanelCommonProps = {
  boot: BootstrapPayload
  overview: Overview
  players: PlayerRow[]
  logs: LogEntry[]
  config: ConfigSnapshot
  draftConfig: ConfigSnapshot
  commandHistory: CommandResult[]
  runtimeActions: PanelRuntimeActions
  runtimeFlags: PanelRuntimeFlags
}
