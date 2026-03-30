import { useDeferredValue, useMemo, useState } from "react"

import type { PanelCommonProps } from "@/app/runtimeTypes"
import { formatRelativeTime, formatTimestamp, getLogTone } from "@/lib/format"
import { cn } from "@/lib/utils"
import { SectionHeader } from "@/components/layout/SectionHeader"
import { EmptyState } from "@/components/panel/EmptyState"
import { Surface } from "@/components/panel/Surface"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Separator } from "@/components/ui/separator"
import { Search } from "lucide-react"

type LogFilter = "all" | "info" | "debug" | "warning" | "error"

export function ActivityPage({ boot, logs }: PanelCommonProps) {
  const [query, setQuery] = useState("")
  const [filter, setFilter] = useState<LogFilter>("all")
  const deferredQuery = useDeferredValue(query)

  const filteredLogs = useMemo(() => {
    const needle = deferredQuery.trim().toLowerCase()

    return logs.filter((entry) => {
      const tone = getLogTone(entry.level)
      if (filter !== "all" && tone !== filter) {
        return false
      }

      if (!needle) {
        return true
      }

      return (
        entry.message.toLowerCase().includes(needle) ||
        entry.level.toLowerCase().includes(needle) ||
        entry.source.toLowerCase().includes(needle)
      )
    })
  }, [deferredQuery, filter, logs])

  const counts = useMemo(() => {
    const next = { info: 0, debug: 0, warning: 0, error: 0 }
    for (const entry of logs) {
      const tone = getLogTone(entry.level)
      if (tone === "error") next.error += 1
      else if (tone === "warning") next.warning += 1
      else if (tone === "debug") next.debug += 1
      else next.info += 1
    }
    return next
  }, [logs])

  const lastLogUtc = logs.length > 0 ? logs[logs.length - 1].timestampUtc : null

  return (
    <div className="grid gap-4">
      <SectionHeader
        title="Activity"
        description="Recent log output mirrored into the panel."
        actions={
          <div className="flex w-full flex-col gap-2 lg:w-auto lg:flex-row lg:items-center">
            <div className="relative w-full max-w-sm">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                className="pl-9"
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Search logs"
                value={query}
              />
            </div>
            <div className="flex flex-wrap gap-2">
              {([
                ["all", "All"],
                ["error", "Errors"],
                ["warning", "Warn"],
                ["debug", "Debug"],
                ["info", "Info"],
              ] as const).map(([id, label]) => (
                <Button
                  key={id}
                  onClick={() => setFilter(id)}
                  size="sm"
                  variant={filter === id ? "secondary" : "outline"}
                >
                  {label}
                </Button>
              ))}
            </div>
          </div>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
        <Surface padding="md">
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-sm font-medium text-foreground">Event stream</p>
              <p className="mt-1 text-xs text-muted-foreground">
                Showing {filteredLogs.length} of {logs.length} buffered entries.
              </p>
            </div>
            <Badge variant="outline">
              Last {lastLogUtc ? formatRelativeTime(lastLogUtc) : "none"}
            </Badge>
          </div>

          <Separator className="my-3" />

          {filteredLogs.length === 0 ? (
            <EmptyState
              title="No activity"
              description="No buffered log entries match the current filters."
            />
          ) : (
            <div className="rounded-md border border-border bg-background/40">
              <ScrollArea className="h-[560px]">
                <div className="grid gap-1 p-2 font-mono text-sm">
                  {filteredLogs
                    .slice(-400)
                    .reverse()
                    .map((entry, index) => {
                      const tone = getLogTone(entry.level)
                      return (
                        <div
                          key={`${entry.timestampUtc}-${index}`}
                          className="grid grid-cols-[78px_58px_minmax(0,1fr)] gap-3 rounded-sm px-2 py-1 hover:bg-muted/40"
                        >
                          <span className="text-xs text-muted-foreground">
                            {new Date(entry.timestampUtc).toLocaleTimeString()}
                          </span>
                          <span
                            className={cn(
                              "text-xs uppercase tracking-[0.14em]",
                              tone === "error"
                                ? "text-destructive"
                                : tone === "warning"
                                  ? "text-amber-200"
                                  : tone === "debug"
                                    ? "text-sky-200"
                                    : "text-emerald-200"
                            )}
                          >
                            {entry.level}
                          </span>
                          <span className="min-w-0 break-words text-foreground">
                            {entry.message}
                          </span>
                        </div>
                      )
                    })}
                </div>
              </ScrollArea>
            </div>
          )}
        </Surface>

        <div className="grid gap-4">
          <Surface padding="md">
            <p className="text-sm font-medium text-foreground">Counts</p>
            <p className="mt-1 text-xs text-muted-foreground">Buffered since start.</p>
            <Separator className="my-3" />
            <div className="grid gap-2 text-sm">
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Errors</span>
                <span className="text-foreground">{counts.error}</span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Warnings</span>
                <span className="text-foreground">{counts.warning}</span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Debug</span>
                <span className="text-foreground">{counts.debug}</span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Info</span>
                <span className="text-foreground">{counts.info}</span>
              </div>
            </div>
          </Surface>

          <Surface padding="md">
            <p className="text-sm font-medium text-foreground">Context</p>
            <p className="mt-1 text-xs text-muted-foreground">
              Local session and file paths.
            </p>
            <Separator className="my-3" />
            <div className="grid gap-3 text-sm">
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Session expires</span>
                <span className="text-foreground">
                  {formatTimestamp(boot.sessionExpiresAtUtc)}
                </span>
              </div>
              <div>
                <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
                  Config path
                </p>
                <p className="mt-1 break-all text-foreground">{boot.configFilePath}</p>
              </div>
            </div>
          </Surface>
        </div>
      </div>
    </div>
  )
}
