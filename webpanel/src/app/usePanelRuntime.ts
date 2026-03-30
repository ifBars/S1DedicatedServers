import { startTransition, useEffect, useMemo, useState } from "react"

import {
  executeCommand,
  exchangeToken,
  getBootstrap,
  getConfig,
  getOverview,
  getPlayers,
  reloadConfig,
  requestShutdown,
  saveConfig as saveConfigApi,
  triggerSave,
  type BootstrapPayload,
  type CommandResult,
  type ConfigSnapshot,
  type LogEntry,
  type Overview,
  type PlayerRow,
} from "@/lib/panel-api"

type ConfigSectionId = keyof ConfigSnapshot

type PanelAction = "save" | "reload" | "shutdown" | null

function cloneConfig(config: ConfigSnapshot) {
  if (typeof structuredClone === "function") {
    return structuredClone(config)
  }

  return JSON.parse(JSON.stringify(config)) as ConfigSnapshot
}

export function usePanelRuntime() {
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [boot, setBoot] = useState<BootstrapPayload | null>(null)
  const [overview, setOverview] = useState<Overview | null>(null)
  const [players, setPlayers] = useState<PlayerRow[]>([])
  const [logs, setLogs] = useState<LogEntry[]>([])

  const [draftConfig, setDraftConfig] = useState<ConfigSnapshot | null>(null)
  const [isSavingConfig, setIsSavingConfig] = useState(false)

  const [runningAction, setRunningAction] = useState<PanelAction>(null)
  const [commandHistory, setCommandHistory] = useState<CommandResult[]>([])
  const [isRunningCommand, setIsRunningCommand] = useState(false)

  const config = boot?.config ?? null

  const isConfigDirty = useMemo(() => {
    if (!config || !draftConfig) {
      return false
    }

    return JSON.stringify(config) !== JSON.stringify(draftConfig)
  }, [config, draftConfig])

  useEffect(() => {
    let active = true

    async function initialize() {
      try {
        const searchParams = new URLSearchParams(window.location.search)
        const launchToken = searchParams.get("token")

        if (launchToken) {
          try {
            await exchangeToken(launchToken)
          } catch {
            // Continue: an existing session may still be valid.
          }

          searchParams.delete("token")
          const nextQuery = searchParams.toString()
          window.history.replaceState(
            {},
            document.title,
            `${window.location.pathname}${nextQuery ? `?${nextQuery}` : ""}`
          )
        }

        const [payload, fetchedPlayers] = await Promise.all([getBootstrap(), getPlayers()])

        if (!payload?.config || !payload?.overview) {
          throw new Error(
            "Panel bootstrap payload was missing expected data. Rebuild the embedded panel assets and restart the server."
          )
        }

        if (!active) {
          return
        }

        startTransition(() => {
          setBoot(payload)
          setOverview(payload.overview)
          setPlayers(fetchedPlayers)
          setDraftConfig(cloneConfig(payload.config))
          setLogs(payload.recentLogs)
          setError(null)
        })
      } catch (caughtError) {
        if (!active) {
          return
        }

        setError(
          caughtError instanceof Error
            ? caughtError.message
            : "Unable to load the localhost panel."
        )
      } finally {
        if (active) {
          setLoading(false)
        }
      }
    }

    void initialize()

    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    if (!boot) {
      return
    }

    const source = new EventSource("/api/events")

    const refreshOverview = () => {
      void getOverview()
        .then((nextOverview) => {
          startTransition(() => {
            setOverview(nextOverview)
          })
        })
        .catch(() => undefined)
    }

    const refreshPlayers = () => {
      void getPlayers()
        .then((nextPlayers) => {
          startTransition(() => {
            setPlayers(nextPlayers)
          })
        })
        .catch(() => undefined)
    }

    const refreshConfig = () => {
      void getConfig()
        .then((nextConfig) => {
          startTransition(() => {
            setBoot((current) =>
              current ? { ...current, config: cloneConfig(nextConfig) } : current
            )
            setDraftConfig(cloneConfig(nextConfig))
          })
        })
        .catch(() => undefined)
    }

    const appendLog = (event: MessageEvent<string>) => {
      try {
        const entry = JSON.parse(event.data) as LogEntry
        startTransition(() => {
          setLogs((current) => [...current.slice(-299), entry])
        })
      } catch {
        // Ignore malformed event payloads.
      }
    }

    source.addEventListener("overview.changed", refreshOverview)
    source.addEventListener("players.changed", refreshPlayers)
    source.addEventListener("config.changed", refreshConfig)
    source.addEventListener("save.changed", refreshOverview)
    source.addEventListener("log.append", appendLog as EventListener)

    return () => {
      source.removeEventListener("overview.changed", refreshOverview)
      source.removeEventListener("players.changed", refreshPlayers)
      source.removeEventListener("config.changed", refreshConfig)
      source.removeEventListener("save.changed", refreshOverview)
      source.removeEventListener("log.append", appendLog as EventListener)
      source.close()
    }
  }, [boot])

  const updateDraftValue = (
    section: ConfigSectionId,
    key: string,
    value: boolean | number | string
  ) => {
    setDraftConfig((current) => {
      if (!current) {
        return current
      }

      const nextSection = {
        ...(current[section] as Record<string, unknown>),
        [key]: value,
      }

      return {
        ...current,
        [section]: nextSection,
      } as ConfigSnapshot
    })
  }

  const resetDraft = () => {
    if (!config) {
      return
    }

    setDraftConfig(cloneConfig(config))
  }

  const saveWorld = async () => {
    setRunningAction("save")
    try {
      await triggerSave()
      setError(null)
    } catch (caughtError) {
      setError(
        caughtError instanceof Error
          ? caughtError.message
          : "Failed to trigger a manual save."
      )
    } finally {
      setRunningAction(null)
    }
  }

  const reloadConfigFromDisk = async () => {
    setRunningAction("reload")
    try {
      const nextConfig = await reloadConfig()
      startTransition(() => {
        setBoot((current) =>
          current ? { ...current, config: cloneConfig(nextConfig) } : current
        )
        setDraftConfig(cloneConfig(nextConfig))
      })
      setError(null)
    } catch (caughtError) {
      setError(
        caughtError instanceof Error
          ? caughtError.message
          : "Failed to reload configuration."
      )
    } finally {
      setRunningAction(null)
    }
  }

  const shutdown = async () => {
    setRunningAction("shutdown")
    try {
      await requestShutdown()
      setError(null)
    } catch (caughtError) {
      setError(
        caughtError instanceof Error
          ? caughtError.message
          : "Failed to request shutdown."
      )
    } finally {
      setRunningAction(null)
    }
  }

  const saveConfig = async () => {
    if (!draftConfig) {
      return
    }

    setIsSavingConfig(true)
    try {
      const nextConfig = await saveConfigApi(draftConfig)
      startTransition(() => {
        setBoot((current) =>
          current ? { ...current, config: cloneConfig(nextConfig) } : current
        )
        setDraftConfig(cloneConfig(nextConfig))
      })
      setError(null)
    } catch (caughtError) {
      setError(
        caughtError instanceof Error
          ? caughtError.message
          : "Failed to save configuration."
      )
    } finally {
      setIsSavingConfig(false)
    }
  }

  const runCommand = async (commandLine: string) => {
    const trimmedCommand = commandLine.trim()
    if (!trimmedCommand) {
      return
    }

    setIsRunningCommand(true)
    try {
      const result = await executeCommand(trimmedCommand)
      startTransition(() => {
        setCommandHistory((current) => [result, ...current].slice(0, 24))
      })
      setError(null)
    } catch (caughtError) {
      setError(
        caughtError instanceof Error
          ? caughtError.message
          : "Command execution failed."
      )
    } finally {
      setIsRunningCommand(false)
    }
  }

  return {
    loading,
    error,
    boot,
    overview,
    players,
    logs,
    config,
    draftConfig,
    isConfigDirty,
    isSavingConfig,
    runningAction,
    commandHistory,
    isRunningCommand,
    actions: {
      updateDraftValue,
      resetDraft,
      saveWorld,
      reloadConfigFromDisk,
      shutdown,
      saveConfig,
      runCommand,
    },
  }
}
