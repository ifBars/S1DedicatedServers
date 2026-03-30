import type { PanelPageId } from "@/app/routes"
import type { PanelCommonProps } from "@/app/runtimeTypes"
import { formatRelativeTime, formatTimestamp, getLogTone } from "@/lib/format"
import { cn } from "@/lib/utils"
import { SectionHeader } from "@/components/layout/SectionHeader"
import { EmptyState } from "@/components/panel/EmptyState"
import { Surface } from "@/components/panel/Surface"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Separator } from "@/components/ui/separator"
import { TerminalSquare } from "lucide-react"

export function OverviewPage({
  boot,
  overview,
  players,
  logs,
  config,
  onNavigate,
}: PanelCommonProps & { onNavigate: (page: PanelPageId) => void }) {
  const recentLogs = logs.slice(-30).reverse()

  const authenticatedCount = players.filter((player) => player.isAuthenticated).length
  const pendingCount = players.filter(
    (player) => player.isAuthenticationPending || player.isModVerificationPending
  ).length

  const occupancy =
    overview.maxPlayers > 0
      ? Math.round((overview.currentPlayers / overview.maxPlayers) * 100)
      : 0

  return (
    <div className="grid gap-4">
      <SectionHeader
        title="Overview"
        description="Key runtime status and the most recent server output."
        actions={
          <Button onClick={() => onNavigate("console")} variant="outline">
            <TerminalSquare data-icon="inline-start" />
            Console
          </Button>
        }
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Surface padding="sm" variant="inset">
          <p className="text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
            Players
          </p>
          <p className="mt-2 text-lg font-semibold text-foreground">
            {overview.currentPlayers}/{overview.maxPlayers}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">{occupancy}% capacity</p>
        </Surface>
        <Surface padding="sm" variant="inset">
          <p className="text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
            Uptime
          </p>
          <p className="mt-2 text-lg font-semibold text-foreground">
            {overview.uptimeDisplay}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">Port {overview.serverPort}</p>
        </Surface>
        <Surface padding="sm" variant="inset">
          <p className="text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
            Auth
          </p>
          <p className="mt-2 text-lg font-semibold text-foreground">
            {authenticatedCount}/{players.length}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">{pendingCount} pending</p>
        </Surface>
        <Surface padding="sm" variant="inset">
          <p className="text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
            Last save
          </p>
          <p className="mt-2 text-lg font-semibold text-foreground">
            {overview.lastSaveUtc ? formatRelativeTime(overview.lastSaveUtc) : "None"}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            {overview.autoSaveEnabled
              ? `Autosave every ${overview.autoSaveIntervalMinutes}m`
              : "Autosave disabled"}
          </p>
        </Surface>
      </div>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
        <Surface padding="md" variant="default">
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="text-sm font-medium text-foreground">Runtime output</p>
              <p className="mt-1 text-xs text-muted-foreground">
                Mirrored from the embedded localhost log stream.
              </p>
            </div>
            <Button onClick={() => onNavigate("activity")} size="sm" variant="outline">
              Activity
            </Button>
          </div>

          <Separator className="my-3" />

          {recentLogs.length === 0 ? (
            <EmptyState
              title="No log entries yet"
              description="Once the server emits runtime output it will appear here."
            />
          ) : (
            <div className="rounded-md border border-border bg-background/40">
              <ScrollArea className="h-[420px]">
                <div className="grid gap-1 p-2 font-mono text-sm">
                  {recentLogs.map((entry, index) => (
                    <div
                      key={`${entry.timestampUtc}-${index}`}
                      className="grid grid-cols-[88px_minmax(0,1fr)] gap-3 rounded-sm px-2 py-1 hover:bg-muted/40"
                    >
                      <span className="text-xs text-muted-foreground">
                        {new Date(entry.timestampUtc).toLocaleTimeString()}
                      </span>
                      <span className="min-w-0 break-words text-foreground">
                        <span
                          className={cn(
                            "mr-2 inline-flex rounded-sm border px-1.5 py-0.5 text-[0.7rem] uppercase tracking-[0.16em]",
                            getLogTone(entry.level) === "error"
                              ? "border-destructive/40 text-destructive"
                              : getLogTone(entry.level) === "warning"
                                ? "border-amber-500/40 text-amber-200"
                                : getLogTone(entry.level) === "debug"
                                  ? "border-sky-500/40 text-sky-200"
                                  : "border-emerald-500/30 text-emerald-200"
                          )}
                        >
                          {entry.level}
                        </span>
                        {entry.message}
                      </span>
                    </div>
                  ))}
                </div>
              </ScrollArea>
            </div>
          )}
        </Surface>

        <div className="grid gap-4">
          <Surface padding="md" variant="default">
            <p className="text-sm font-medium text-foreground">At a glance</p>
            <p className="mt-1 text-xs text-muted-foreground">
              Operators, permissions, and auth posture.
            </p>
            <Separator className="my-3" />
            <div className="grid gap-3 text-sm">
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Auth provider</span>
                <span className="text-foreground">{overview.authProvider}</span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Groups</span>
                <span className="text-foreground">{overview.totalGroups}</span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Operators</span>
                <span className="text-foreground">{overview.totalOperators}</span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Admins</span>
                <span className="text-foreground">{overview.totalAdministrators}</span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Bans</span>
                <span className="text-foreground">{overview.totalBans}</span>
              </div>
            </div>
          </Surface>

          <Surface padding="md" variant="default">
            <p className="text-sm font-medium text-foreground">Paths</p>
            <p className="mt-1 text-xs text-muted-foreground">
              Files involved in the current session.
            </p>
            <Separator className="my-3" />
            <div className="grid gap-3 text-sm">
              <div>
                <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
                  Config
                </p>
                <p className="mt-1 break-all text-foreground">{boot.configFilePath}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
                  Permissions
                </p>
                <p className="mt-1 break-all text-foreground">
                  {boot.permissionsFilePath}
                </p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
                  Save path
                </p>
                <p className="mt-1 break-all text-foreground">
                  {config.storage.resolvedSaveGamePath}
                </p>
              </div>
              <div className="flex flex-wrap gap-2 pt-2">
                <Badge variant="outline">
                  Session expires {formatTimestamp(boot.sessionExpiresAtUtc)}
                </Badge>
              </div>
            </div>
          </Surface>
        </div>
      </div>
    </div>
  )
}
