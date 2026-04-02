import { useState } from "react"

import type { PanelCommonProps } from "@/app/runtimeTypes"
import { SectionHeader } from "@/components/layout/SectionHeader"
import { LogStream } from "@/components/panel/LogStream"
import { Surface } from "@/components/panel/Surface"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Separator } from "@/components/ui/separator"
import { TerminalSquare } from "lucide-react"

const CONSOLE_EXAMPLES = ["serverinfo", "save", "listplayers", "help"]

export function ConsolePage({
  logs,
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

      <div className="grid gap-4">
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

          <LogStream
            emptyDescription="Waiting for server output."
            emptyTitle="No log entries yet"
            heightClassName="h-[520px]"
            logs={recentLogs}
            maxEntries={400}
            variant="columns"
          />

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
      </div>
    </div>
  )
}
