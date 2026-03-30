import type { LucideIcon } from "lucide-react"
import {
  Activity,
  LayoutDashboard,
  Settings2,
  TerminalSquare,
  Users,
} from "lucide-react"

export type PanelPageId = "overview" | "console" | "players" | "config" | "activity"

export type PanelRoute = {
  id: PanelPageId
  path: string
  label: string
  icon: LucideIcon
}

export type PanelRouteGroup = {
  id: "server" | "configuration"
  label: string
  routes: PanelRoute[]
}

export const PANEL_ROUTE_GROUPS: PanelRouteGroup[] = [
  {
    id: "server",
    label: "Server",
    routes: [
      { id: "overview", path: "/", label: "Overview", icon: LayoutDashboard },
      { id: "console", path: "/console", label: "Console", icon: TerminalSquare },
      { id: "players", path: "/players", label: "Players", icon: Users },
      { id: "activity", path: "/activity", label: "Activity", icon: Activity },
    ],
  },
  {
    id: "configuration",
    label: "Configuration",
    routes: [{ id: "config", path: "/config", label: "Config", icon: Settings2 }],
  },
]

export const PANEL_ROUTES: PanelRoute[] = PANEL_ROUTE_GROUPS.flatMap(
  (group) => group.routes
)

const PATH_TO_PAGE = PANEL_ROUTES.reduce<Record<string, PanelPageId>>(
  (paths, route) => {
    paths[route.path.toLowerCase()] = route.id
    return paths
  },
  {}
)

export function getPageFromPath(pathname: string): PanelPageId {
  const normalizedPath = pathname.replace(/\/+$/, "") || "/"
  return PATH_TO_PAGE[normalizedPath.toLowerCase()] ?? "overview"
}

export function getPathForPage(page: PanelPageId) {
  return PANEL_ROUTES.find((route) => route.id === page)?.path ?? "/"
}

export function getRouteForPage(page: PanelPageId) {
  return PANEL_ROUTES.find((route) => route.id === page) ?? PANEL_ROUTES[0]
}
