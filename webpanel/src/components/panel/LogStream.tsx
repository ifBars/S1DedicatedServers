import { useEffect, useMemo, useRef } from "react"

import type { LogEntry } from "@/lib/panel-api"
import { getLogTone } from "@/lib/format"
import { cn } from "@/lib/utils"
import { EmptyState } from "@/components/panel/EmptyState"
import { ScrollArea } from "@/components/ui/scroll-area"

type LogStreamVariant = "columns" | "badge"

type LogStreamProps = {
  logs: LogEntry[]
  maxEntries: number
  heightClassName: string
  className?: string
  emptyTitle: string
  emptyDescription: string
  followTail?: boolean
  variant?: LogStreamVariant
}

export function LogStream({
  logs,
  maxEntries,
  heightClassName,
  className,
  emptyTitle,
  emptyDescription,
  followTail = true,
  variant = "columns",
}: LogStreamProps) {
  const scrollAreaRef = useRef<HTMLDivElement | null>(null)

  const visibleLogs = useMemo(() => logs.slice(-maxEntries), [logs, maxEntries])

  useEffect(() => {
    if (!followTail) {
      return
    }

    const viewport = scrollAreaRef.current?.querySelector<HTMLElement>(
      "[data-slot='scroll-area-viewport']"
    )

    if (!viewport) {
      return
    }

    viewport.scrollTop = viewport.scrollHeight
  }, [followTail, visibleLogs.length])

  if (visibleLogs.length === 0) {
    return (
      <EmptyState title={emptyTitle} description={emptyDescription} />
    )
  }

  return (
    <div className={cn("rounded-md border border-border bg-background/40", className)}>
      <ScrollArea className={heightClassName} ref={scrollAreaRef}>
        <div className="grid gap-1 p-2 font-mono text-sm">
          {visibleLogs.map((entry, index) =>
            variant === "badge" ? (
              <BadgeRow key={`${entry.timestampUtc}-${index}`} entry={entry} />
            ) : (
              <ColumnRow key={`${entry.timestampUtc}-${index}`} entry={entry} />
            )
          )}
        </div>
      </ScrollArea>
    </div>
  )
}

function ColumnRow({ entry }: { entry: LogEntry }) {
  const tone = getLogTone(entry.level)

  return (
    <div className="grid grid-cols-[72px_52px_minmax(0,1fr)] gap-3 rounded-sm px-2 py-1 hover:bg-muted/40">
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
      <span className="min-w-0 break-words whitespace-pre-wrap text-foreground">
        {entry.message}
      </span>
    </div>
  )
}

function BadgeRow({ entry }: { entry: LogEntry }) {
  const tone = getLogTone(entry.level)

  return (
    <div className="grid grid-cols-[88px_minmax(0,1fr)] gap-3 rounded-sm px-2 py-1 hover:bg-muted/40">
      <span className="text-xs text-muted-foreground">
        {new Date(entry.timestampUtc).toLocaleTimeString()}
      </span>
      <span className="min-w-0 break-words whitespace-pre-wrap text-foreground">
        <span
          className={cn(
            "mr-2 inline-flex rounded-sm border px-1.5 py-0.5 text-[0.7rem] uppercase tracking-[0.16em]",
            tone === "error"
              ? "border-destructive/40 text-destructive"
              : tone === "warning"
                ? "border-amber-500/40 text-amber-200"
                : tone === "debug"
                  ? "border-sky-500/40 text-sky-200"
                  : "border-emerald-500/30 text-emerald-200"
          )}
        >
          {entry.level}
        </span>
        {entry.message}
      </span>
    </div>
  )
}
