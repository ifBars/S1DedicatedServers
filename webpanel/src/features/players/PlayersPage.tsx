import { useDeferredValue, useMemo, useState } from "react"

import type { PanelCommonProps } from "@/app/runtimeTypes"
import { getPlayerStateBadgeVariant, getPlayerStateLabel, formatTimestamp } from "@/lib/format"
import { SectionHeader } from "@/components/layout/SectionHeader"
import { Surface } from "@/components/panel/Surface"
import { Badge } from "@/components/ui/badge"
import { Input } from "@/components/ui/input"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Separator } from "@/components/ui/separator"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Search } from "lucide-react"

export function PlayersPage({ overview, players }: PanelCommonProps) {
  const [query, setQuery] = useState("")
  const deferredQuery = useDeferredValue(query)

  const filteredPlayers = useMemo(() => {
    const needle = deferredQuery.trim().toLowerCase()
    if (!needle) {
      return players
    }

    return players.filter((player) => {
      return (
        player.displayName.toLowerCase().includes(needle) ||
        player.steamId.toLowerCase().includes(needle) ||
        player.trustedUniqueId.toLowerCase().includes(needle) ||
        player.roleSummary.toLowerCase().includes(needle)
      )
    })
  }, [deferredQuery, players])

  const authenticatedCount = players.filter((player) => player.isAuthenticated).length
  const pendingCount = players.filter(
    (player) => player.isAuthenticationPending || player.isModVerificationPending
  ).length
  const spawnedCount = players.filter((player) => player.isSpawned).length

  return (
    <div className="grid gap-4">
      <SectionHeader
        title="Players"
        description="Inspect connected sessions and authentication state."
        actions={
          <div className="relative w-full max-w-sm">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="pl-9"
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Search players"
              value={query}
            />
          </div>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
        <Surface padding="md">
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="text-sm font-medium text-foreground">Current sessions</p>
              <p className="mt-1 text-xs text-muted-foreground">
                {filteredPlayers.length} of {players.length} records match the current filter.
              </p>
            </div>
            <Badge variant="outline">
              {overview.currentPlayers}/{overview.maxPlayers} online
            </Badge>
          </div>

          <Separator className="my-3" />

          <ScrollArea className="w-full">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Player</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Steam</TableHead>
                  <TableHead>Session</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredPlayers.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} className="py-10 text-center">
                      <p className="text-sm text-muted-foreground">
                        No players match the current filter.
                      </p>
                    </TableCell>
                  </TableRow>
                ) : (
                  filteredPlayers.map((player) => (
                    <TableRow key={`${player.clientId}-${player.steamId}`}>
                      <TableCell className="align-top">
                        <div className="grid gap-1">
                          <p className="font-medium text-foreground">
                            {player.displayName}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            Client {player.clientId}
                          </p>
                        </div>
                      </TableCell>
                      <TableCell className="align-top">
                        <div className="flex flex-col gap-2">
                          <Badge variant={getPlayerStateBadgeVariant(player)}>
                            {getPlayerStateLabel(player)}
                          </Badge>
                          <span className="text-xs text-muted-foreground">
                            {player.isSpawned ? "Spawned" : "Not spawned"}
                          </span>
                        </div>
                      </TableCell>
                      <TableCell className="align-top">
                        <p className="text-sm text-foreground">
                          {player.roleSummary || "No explicit role"}
                        </p>
                        <p className="mt-1 text-xs text-muted-foreground">
                          {player.isLoopback ? "Loopback connection" : player.trustedUniqueId}
                        </p>
                      </TableCell>
                      <TableCell className="align-top">
                        <p className="break-all text-sm text-muted-foreground">
                          {player.steamId}
                        </p>
                      </TableCell>
                      <TableCell className="align-top">
                        <p className="text-sm text-foreground">{player.connectionDuration}</p>
                        <p className="mt-1 text-xs text-muted-foreground">
                          Since {formatTimestamp(player.connectedAtUtc)}
                        </p>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </ScrollArea>
        </Surface>

        <div className="grid gap-4">
          <Surface padding="md">
            <p className="text-sm font-medium text-foreground">Summary</p>
            <p className="mt-1 text-xs text-muted-foreground">
              Presence and trust completion.
            </p>
            <Separator className="my-3" />
            <div className="grid gap-2 text-sm">
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Online</span>
                <span className="text-foreground">
                  {overview.currentPlayers}/{overview.maxPlayers}
                </span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Authenticated</span>
                <span className="text-foreground">{authenticatedCount}</span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Pending</span>
                <span className="text-foreground">{pendingCount}</span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Spawned</span>
                <span className="text-foreground">{spawnedCount}</span>
              </div>
            </div>
          </Surface>
        </div>
      </div>
    </div>
  )
}
