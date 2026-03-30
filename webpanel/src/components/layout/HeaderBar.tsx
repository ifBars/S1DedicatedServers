import type { BootstrapPayload, Overview } from "@/lib/panel-api"
import { formatFrameRate, formatRelativeTime } from "@/lib/format"
import { cn } from "@/lib/utils"
import { Surface } from "@/components/panel/Surface"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Power, RefreshCw, Save } from "lucide-react"

export function HeaderBar({
  boot,
  overview,
  pageLabel,
  leftAddon,
  isBusy,
  onSaveWorld,
  onReloadConfig,
  onRequestShutdown,
}: {
  boot: BootstrapPayload
  overview: Overview
  pageLabel: string
  leftAddon?: React.ReactNode
  isBusy: boolean
  onSaveWorld: () => void
  onReloadConfig: () => void
  onRequestShutdown: () => void
}) {
  const lastSaveLabel = overview.lastSaveUtc
    ? formatRelativeTime(overview.lastSaveUtc)
    : "Not recorded"

  return (
    <Surface padding="md" className="flex flex-col gap-3">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="flex items-start gap-3">
          {leftAddon ? <div className="lg:hidden">{leftAddon}</div> : null}
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2 text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
              <span>Servers</span>
              <span className="text-muted-foreground/60">/</span>
              <span>Local</span>
              <span className="text-muted-foreground/60">/</span>
              <span className="text-foreground">{pageLabel}</span>
            </div>

            <div className="mt-2 flex flex-wrap items-center gap-3">
              <h1 className="truncate text-xl font-semibold text-foreground">
                {overview.serverName}
              </h1>
              <div className="flex items-center gap-2">
                <span
                  className={cn(
                    "size-2 rounded-full",
                    overview.isRunning ? "bg-emerald-400" : "bg-muted-foreground"
                  )}
                />
                <span className="text-sm text-muted-foreground">
                  {overview.status}
                </span>
              </div>
            </div>

            <div className="mt-3 flex flex-wrap gap-2">
              <Badge variant="outline">Port {overview.serverPort}</Badge>
              <Badge variant="outline">{formatFrameRate(overview.framesPerSecond)}</Badge>
              <Badge variant="outline">{overview.authProvider}</Badge>
              <Badge variant="outline">{boot.version}</Badge>
            </div>
          </div>
        </div>

        <div className="flex flex-wrap items-center justify-start gap-2 lg:justify-end">
          <Button disabled={isBusy} onClick={onSaveWorld} variant="secondary">
            <Save data-icon="inline-start" />
            Save
          </Button>
          <Button disabled={isBusy} onClick={onReloadConfig} variant="outline">
            <RefreshCw data-icon="inline-start" />
            Reload
          </Button>
          <Button disabled={isBusy} onClick={onRequestShutdown} variant="destructive">
            <Power data-icon="inline-start" />
            Shutdown
          </Button>
        </div>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3 text-xs text-muted-foreground">
        <span>
          Players {overview.currentPlayers}/{overview.maxPlayers}
        </span>
        <span>Uptime {overview.uptimeDisplay}</span>
        <span>Last save {lastSaveLabel}</span>
      </div>
    </Surface>
  )
}
