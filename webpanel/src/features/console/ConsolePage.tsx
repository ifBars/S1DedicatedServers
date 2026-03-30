import { useState } from "react"

import type { PanelCommonProps } from "@/app/runtimeTypes"
import { formatTimestamp, getLogTone } from "@/lib/format"
import { cn } from "@/lib/utils"
import { SectionHeader } from "@/components/layout/SectionHeader"
import { Surface } from "@/components/panel/Surface"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Separator } from "@/components/ui/separator"
import { TerminalSquare } from "lucide-react"

const CONSOLE_EXAMPLES = ["serverinfo", "save", "listplayers", "help"]

export function ConsolePage({
  overview,
  logs,
  commandHistory,
  runtimeActions,
  runtimeFlags,
}: PanelCommonProps) {
  const [commandLine, setCommandLine] = useState("")

  const recentLogs = logs.slice(-400)

  const run = async () => {
    const nextCommand = commandLine.trim()
    if (!nextCommand) {
      return
    }

    await runtimeActions.runCommand(nextCommand)
    setCommandLine("")
  }

  return (
    <div className="grid gap-4">
      <SectionHeader
        title="Console"
        description="Live runtime output. Commands execute through the same server command pipeline as TCP/stdio."
        actions={
          <>
            {CONSOLE_EXAMPLES.map((example) => (
              <Button
                key={example}
                onClick={() => setCommandLine(example)}
                size="sm"
                variant="outline"
              >
                {example}
              </Button>
            ))}
          </>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
        <Surface padding="md">
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-sm font-medium text-foreground">Live output</p>
              <p className="mt-1 text-xs text-muted-foreground">
                Streamed via the embedded Server-Sent Events endpoint.
              </p>
            </div>
            <Badge variant="outline">{recentLogs.length} lines</Badge>
          </div>

          <Separator className="my-3" />

          <div className="rounded-md border border-border bg-background/40">
            <ScrollArea className="h-[520px]">
              <div className="grid gap-1 p-2 font-mono text-sm">
                {recentLogs.length === 0 ? (
                  <p className="p-2 text-sm text-muted-foreground">
                    Waiting for server output.
                  </p>
                ) : (
                  recentLogs.map((entry, index) => (
                    <div
                      key={`${entry.timestampUtc}-${index}`}
                      className="grid grid-cols-[72px_52px_minmax(0,1fr)] gap-3 rounded-sm px-2 py-1 hover:bg-muted/40"
                    >
                      <span className="text-xs text-muted-foreground">
                        {new Date(entry.timestampUtc).toLocaleTimeString()}
                      </span>
                      <span
                        className={cn(
                          "text-xs uppercase tracking-[0.14em]",
                          getLogTone(entry.level) === "error"
                            ? "text-destructive"
                            : getLogTone(entry.level) === "warning"
                              ? "text-amber-200"
                              : getLogTone(entry.level) === "debug"
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
                  ))
                )}
              </div>
            </ScrollArea>
          </div>

          <div className="mt-3 flex flex-col gap-3 md:flex-row">
            <Input
              className="font-mono"
              onChange={(event) => setCommandLine(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault()
                  void run()
                }
              }}
              placeholder="Type a server command"
              value={commandLine}
            />
            <Button
              disabled={runtimeFlags.isRunningCommand}
              onClick={() => void run()}
            >
              <TerminalSquare data-icon="inline-start" />
              {runtimeFlags.isRunningCommand ? "Running..." : "Run"}
            </Button>
          </div>
        </Surface>

        <div className="grid gap-4">
          <Surface padding="md">
            <p className="text-sm font-medium text-foreground">Runtime</p>
            <p className="mt-1 text-xs text-muted-foreground">
              Key stats while operating from the console.
            </p>
            <Separator className="my-3" />
            <div className="grid gap-3 text-sm">
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Uptime</span>
                <span className="text-foreground">{overview.uptimeDisplay}</span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Players</span>
                <span className="text-foreground">
                  {overview.currentPlayers}/{overview.maxPlayers}
                </span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <span className="text-muted-foreground">Last save</span>
                <span className="text-foreground">
                  {formatTimestamp(overview.lastSaveUtc)}
                </span>
              </div>
            </div>
          </Surface>

          <Surface padding="md">
            <p className="text-sm font-medium text-foreground">Command history</p>
            <p className="mt-1 text-xs text-muted-foreground">
              Results returned by the server-side command bridge.
            </p>
            <Separator className="my-3" />
            {commandHistory.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                No commands executed from the panel yet.
              </p>
            ) : (
              <div className="rounded-md border border-border bg-background/30">
                <div className="divide-y divide-border">
                  {commandHistory.map((result, index) => (
                    <div key={`${result.commandWord}-${index}`} className="p-3">
                      <div className="flex flex-wrap items-center gap-2">
                        <Badge
                          variant={result.succeeded ? "secondary" : "destructive"}
                        >
                          {result.succeeded ? "OK" : "FAIL"}
                        </Badge>
                        <span className="text-sm font-medium text-foreground">
                          {result.commandWord || "command"}
                        </span>
                        <span className="ml-auto text-xs text-muted-foreground">
                          {result.status}
                        </span>
                      </div>
                      {result.message ? (
                        <p className="mt-2 text-xs text-muted-foreground">
                          {result.message}
                        </p>
                      ) : null}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </Surface>
        </div>
      </div>
    </div>
  )
}
