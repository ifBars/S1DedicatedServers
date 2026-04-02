import { useEffect, useMemo, useState } from "react"

import type { PanelCommonProps } from "@/app/runtimeTypes"
import {
  CONFIG_SECTIONS,
  getConfigSection,
  type ConfigFieldDefinition,
  type ConfigSectionId,
} from "@/features/config/configSchema"
import { cn } from "@/lib/utils"
import { SectionHeader } from "@/components/layout/SectionHeader"
import { Surface } from "@/components/panel/Surface"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Separator } from "@/components/ui/separator"
import { Switch } from "@/components/ui/switch"
import { Textarea } from "@/components/ui/textarea"
import { RotateCcw, Save, UploadCloud } from "lucide-react"

type FieldRowProps = {
  field: ConfigFieldDefinition
  value: unknown
  onChange: (nextValue: boolean | number | string) => void
}

function BooleanRow({ field, value, onChange }: Omit<FieldRowProps, "sectionId">) {
  const checked = Boolean(value)

  return (
    <div className="flex items-center justify-between gap-3">
      <div className="flex flex-col gap-1">
        <span className="text-sm font-medium text-foreground">{field.label}</span>
        <span className="text-xs text-muted-foreground">{field.description}</span>
      </div>
      <div className="flex items-center gap-3">
        <span className="text-xs text-muted-foreground">
          {checked ? field.trueLabel ?? "Enabled" : field.falseLabel ?? "Disabled"}
        </span>
        <Switch
          checked={checked}
          disabled={field.readOnly}
          onCheckedChange={(next) => onChange(next)}
        />
      </div>
    </div>
  )
}

function NumberRow({
  field,
  value,
  onChange,
}: Omit<FieldRowProps, "sectionId">) {
  const [draft, setDraft] = useState(() =>
    typeof value === "number" && !Number.isNaN(value) ? String(value) : ""
  )

  useEffect(() => {
    setDraft(typeof value === "number" && !Number.isNaN(value) ? String(value) : "")
  }, [value])

  const commit = () => {
    const trimmed = draft.trim()
    if (!trimmed) {
      setDraft(typeof value === "number" && !Number.isNaN(value) ? String(value) : "")
      return
    }

    const parsed = Number(trimmed)
    if (Number.isNaN(parsed)) {
      setDraft(typeof value === "number" && !Number.isNaN(value) ? String(value) : "")
      return
    }

    onChange(parsed)
  }

  return (
    <div className="grid gap-2 md:grid-cols-[280px_minmax(0,1fr)] md:items-start">
      <div className="min-w-0">
        <p className="text-sm font-medium text-foreground">{field.label}</p>
        <p className="mt-1 text-xs text-muted-foreground">{field.description}</p>
      </div>
      <Input
        disabled={field.readOnly}
        inputMode="numeric"
        min={field.min}
        onBlur={commit}
        onChange={(event) => setDraft(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            event.preventDefault()
            commit()
          }
        }}
        placeholder={field.placeholder}
        step={field.step}
        type="number"
        value={draft}
      />
    </div>
  )
}

function TextRow({
  field,
  value,
  onChange,
}: Omit<FieldRowProps, "sectionId">) {
  return (
    <div className="grid gap-2 md:grid-cols-[280px_minmax(0,1fr)] md:items-start">
      <div className="min-w-0">
        <p className="text-sm font-medium text-foreground">{field.label}</p>
        <p className="mt-1 text-xs text-muted-foreground">{field.description}</p>
      </div>
      <Input
        disabled={field.readOnly}
        onChange={(event) => onChange(event.target.value)}
        placeholder={field.placeholder}
        type={field.kind === "password" ? "password" : "text"}
        value={typeof value === "string" ? value : ""}
      />
    </div>
  )
}

function TextareaRow({
  field,
  value,
  onChange,
}: Omit<FieldRowProps, "sectionId">) {
  return (
    <div className="grid gap-2 md:grid-cols-[280px_minmax(0,1fr)] md:items-start">
      <div className="min-w-0">
        <p className="text-sm font-medium text-foreground">{field.label}</p>
        <p className="mt-1 text-xs text-muted-foreground">{field.description}</p>
      </div>
      <Textarea
        disabled={field.readOnly}
        onChange={(event) => onChange(event.target.value)}
        placeholder={field.placeholder}
        value={typeof value === "string" ? value : ""}
      />
    </div>
  )
}

function FieldRow({ field, value, onChange }: FieldRowProps) {
  if (field.kind === "boolean") {
    return <BooleanRow field={field} onChange={onChange} value={value} />
  }

  if (field.kind === "number") {
    return <NumberRow field={field} onChange={onChange} value={value} />
  }

  if (field.kind === "textarea") {
    return <TextareaRow field={field} onChange={onChange} value={value} />
  }

  if (field.kind === "password") {
    return <TextRow field={field} onChange={onChange} value={value} />
  }

  return <TextRow field={field} onChange={onChange} value={value} />
}

export function ConfigPage({
  draftConfig,
  runtimeActions,
  runtimeFlags,
}: PanelCommonProps) {
  const [activeSection, setActiveSection] = useState<ConfigSectionId>("server")

  const section = useMemo(
    () => getConfigSection(activeSection),
    [activeSection]
  )

  const sectionData = draftConfig[section.id] as Record<string, unknown>

  return (
    <div className="grid gap-4">
      <SectionHeader
        title="Config"
        description="Edit server configuration and persist changes to disk."
        actions={
          <>
            {runtimeFlags.isConfigDirty ? (
              <Badge variant="outline">Unsaved changes</Badge>
            ) : (
              <Badge variant="secondary">Saved</Badge>
            )}
            <Button
              disabled={!runtimeFlags.isConfigDirty || runtimeFlags.isSavingConfig}
              onClick={() => void runtimeActions.saveConfig()}
            >
              <Save data-icon="inline-start" />
              {runtimeFlags.isSavingConfig ? "Saving..." : "Save config"}
            </Button>
            <Button
              disabled={!runtimeFlags.isConfigDirty || runtimeFlags.isBusy}
              onClick={runtimeActions.resetDraft}
              variant="outline"
            >
              <RotateCcw data-icon="inline-start" />
              Reset
            </Button>
            <Button
              disabled={runtimeFlags.isBusy}
              onClick={() => void runtimeActions.reloadConfigFromDisk()}
              variant="outline"
            >
              <UploadCloud data-icon="inline-start" />
              Reload from disk
            </Button>
          </>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[260px_minmax(0,1fr)]">
        <Surface padding="none" className="overflow-hidden">
          <div className="border-b border-border px-4 py-3">
            <p className="text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
              Sections
            </p>
          </div>
          <ScrollArea className="h-[540px]">
            <div className="p-2">
              {CONFIG_SECTIONS.map((next) => {
                const active = next.id === activeSection
                return (
                  <button
                    key={next.id}
                    className={cn(
                      "flex w-full flex-col gap-1 rounded-md border border-transparent px-3 py-2 text-left transition-colors",
                      active
                        ? "border-primary/30 bg-muted text-foreground"
                        : "text-muted-foreground hover:bg-muted/40 hover:text-foreground"
                    )}
                    onClick={() => setActiveSection(next.id)}
                    type="button"
                  >
                    <span className="text-sm font-medium">{next.label}</span>
                    <span className="text-xs text-muted-foreground">
                      {next.description}
                    </span>
                  </button>
                )
              })}
            </div>
          </ScrollArea>
        </Surface>

        <Surface padding="md">
          <div className="flex flex-col gap-1">
            <p className="text-sm font-medium text-foreground">{section.label}</p>
            <p className="text-xs text-muted-foreground">{section.description}</p>
          </div>

          <Separator className="my-3" />

          <div className="grid divide-y divide-border">
            {section.fields.map((field) => (
              <div key={`${section.id}.${field.key}`} className="py-3">
                <FieldRow
                  field={field}
                  value={sectionData?.[field.key]}
                  onChange={(nextValue) => {
                    runtimeActions.updateDraftValue(section.id, field.key, nextValue)
                  }}
                />
              </div>
            ))}
          </div>
        </Surface>
      </div>
    </div>
  )
}
