import { useEffect, useRef } from "react"
import { toast } from "sonner"

import { EmptyState } from "@/components/panel/EmptyState"
import { AppShell } from "@/app/AppShell"
import { usePanelRouter } from "@/app/usePanelRouter"
import { usePanelRuntime } from "@/app/usePanelRuntime"
import { ActivityPage } from "@/features/activity/ActivityPage"
import { ConsolePage } from "@/features/console/ConsolePage"
import { ConfigPage } from "@/features/config/ConfigPage"
import { OverviewPage } from "@/features/overview/OverviewPage"
import { PlayersPage } from "@/features/players/PlayersPage"

export default function App() {
  const { page, navigate } = usePanelRouter()
  const runtime = usePanelRuntime()
  const lastToastErrorRef = useRef<string | null>(null)

  useEffect(() => {
    if (!runtime.boot || !runtime.error) {
      lastToastErrorRef.current = runtime.error
      return
    }

    if (lastToastErrorRef.current === runtime.error) {
      return
    }

    toast.error("Request failed", {
      description: runtime.error,
    })
    lastToastErrorRef.current = runtime.error
  }, [runtime.boot, runtime.error])

  if (runtime.loading) {
    return (
      <div className="p-6">
        <EmptyState
          title="Connecting to localhost panel"
          description="Waiting for the embedded server API to answer."
        />
      </div>
    )
  }

  if (runtime.error && !runtime.boot) {
    return (
      <div className="p-6">
        <EmptyState
          title="Local authorization required"
          description="This panel only accepts localhost sessions created from the one-time launch URL printed by the server."
          details={runtime.error}
        />
      </div>
    )
  }

  if (!runtime.boot || !runtime.overview || !runtime.config || !runtime.draftConfig) {
    return null
  }

  const isBusy =
    runtime.runningAction !== null ||
    runtime.isSavingConfig ||
    runtime.isRunningCommand

  const common = {
    boot: runtime.boot,
    overview: runtime.overview,
    players: runtime.players,
    logs: runtime.logs,
    config: runtime.config,
    draftConfig: runtime.draftConfig,
    commandHistory: runtime.commandHistory,
    runtimeActions: runtime.actions,
    runtimeFlags: {
      isBusy,
      isConfigDirty: runtime.isConfigDirty,
      isSavingConfig: runtime.isSavingConfig,
      runningAction: runtime.runningAction,
      isRunningCommand: runtime.isRunningCommand,
    },
  }

  let content: React.ReactNode

  switch (page) {
    case "console":
      content = <ConsolePage {...common} />
      break
    case "players":
      content = <PlayersPage {...common} />
      break
    case "config":
      content = <ConfigPage {...common} />
      break
    case "activity":
      content = <ActivityPage {...common} />
      break
    case "overview":
    default:
      content = <OverviewPage {...common} onNavigate={navigate} />
      break
  }

  return (
    <AppShell
      boot={runtime.boot}
      isBusy={isBusy}
      onNavigate={navigate}
      onReloadConfig={runtime.actions.reloadConfigFromDisk}
      onSaveWorld={runtime.actions.saveWorld}
      onShutdown={runtime.actions.shutdown}
      overview={runtime.overview}
      page={page}
    >
      {content}
    </AppShell>
  )
}
