import type { BootstrapPayload, Overview } from "@/lib/panel-api"
import { cn } from "@/lib/utils"
import { Surface } from "@/components/panel/Surface"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { PANEL_ROUTE_GROUPS, type PanelPageId } from "@/app/routes"

export function Sidebar({
  page,
  boot,
  overview,
  onNavigate,
}: {
  page: PanelPageId
  boot: BootstrapPayload | null
  overview: Overview | null
  onNavigate: (page: PanelPageId) => void
}) {
  return (
    <div className="flex h-full flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground">
      <div className="flex items-center gap-3 px-4 py-4">
        <div className="flex size-9 items-center justify-center rounded-md bg-muted text-foreground">
          <span className="text-sm font-semibold">S1</span>
        </div>
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-foreground">
            Dedicated Server
          </p>
          <p className="truncate text-xs text-muted-foreground">
            Local control panel
          </p>
        </div>
      </div>

      <div className="px-3">
        <Surface padding="sm" variant="inset">
          {overview ? (
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium text-foreground">
                  {overview.serverName}
                </p>
                <p className="mt-1 text-xs text-muted-foreground">
                  Port {overview.serverPort} | {overview.currentPlayers}/{overview.maxPlayers} online
                </p>
              </div>
              <Badge variant="outline" className="shrink-0">
                {overview.status}
              </Badge>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">No server connected.</p>
          )}
        </Surface>
      </div>

      <nav className="flex min-h-0 flex-1 flex-col gap-4 overflow-auto px-2 py-4">
        {PANEL_ROUTE_GROUPS.map((group) => (
          <div key={group.id} className="flex flex-col gap-1">
            <p className="px-3 py-1 text-[0.65rem] font-medium uppercase tracking-[0.2em] text-muted-foreground">
              {group.label}
            </p>
            {group.routes.map((route) => {
              const Icon = route.icon
              const active = route.id === page

              return (
                <Button
                  key={route.id}
                  variant="ghost"
                  className={cn(
                    "relative h-9 justify-start gap-2 rounded-md px-3 text-sm",
                    active
                      ? "bg-muted text-foreground before:absolute before:inset-y-1 before:left-0 before:w-0.5 before:rounded before:bg-primary"
                      : "text-muted-foreground hover:bg-muted/50 hover:text-foreground"
                  )}
                  onClick={() => onNavigate(route.id)}
                >
                  <Icon data-icon="inline-start" />
                  {route.label}
                </Button>
              )
            })}
          </div>
        ))}
      </nav>

      <div className="border-t border-border px-4 py-3 text-xs text-muted-foreground">
        {boot?.version ? `Version ${boot.version}` : "Version unavailable"}
      </div>
    </div>
  )
}
