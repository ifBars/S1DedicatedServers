import type { PlayerRow } from "@/lib/panel-api"

const timestampFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
})

export function formatTimestamp(value: string | null) {
  if (!value) {
    return "Not available"
  }

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return "Invalid date"
  }

  return timestampFormatter.format(parsed)
}

export function formatRelativeTime(value: string | null) {
  if (!value) {
    return "Not recorded"
  }

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return "Unavailable"
  }

  const deltaSeconds = Math.max(
    0,
    Math.floor((Date.now() - parsed.getTime()) / 1000)
  )

  if (deltaSeconds < 60) {
    return `${deltaSeconds}s ago`
  }

  const deltaMinutes = Math.floor(deltaSeconds / 60)
  if (deltaMinutes < 60) {
    return `${deltaMinutes}m ago`
  }

  const deltaHours = Math.floor(deltaMinutes / 60)
  if (deltaHours < 24) {
    return `${deltaHours}h ago`
  }

  return `${Math.floor(deltaHours / 24)}d ago`
}

export function formatFrameRate(value: number) {
  if (!Number.isFinite(value) || value <= 0) {
    return "Sampling"
  }

  return `${value.toFixed(1)} FPS`
}

export function formatFrameTime(value: number) {
  if (!Number.isFinite(value) || value <= 0) {
    return "Waiting for samples"
  }

  return `${value.toFixed(1)} ms/frame`
}

export type LogTone = "info" | "debug" | "success" | "warning" | "error"

export function getLogTone(level: string): LogTone {
  switch (level) {
    case "error":
      return "error"
    case "warning":
      return "warning"
    case "debug":
      return "debug"
    default:
      return "info"
  }
}

export function getPlayerStateLabel(player: PlayerRow) {
  if (player.isLoopback) {
    return "Loopback"
  }

  if (player.isAuthenticationPending || player.isModVerificationPending) {
    return "Pending"
  }

  if (player.isAuthenticated) {
    return "Authenticated"
  }

  if (player.isConnected) {
    return "Connected"
  }

  return "Offline"
}

export function getPlayerStateBadgeVariant(player: PlayerRow) {
  if (player.isLoopback) {
    return "secondary" as const
  }

  if (player.isAuthenticationPending || player.isModVerificationPending) {
    return "outline" as const
  }

  if (player.isAuthenticated) {
    return "default" as const
  }

  return "secondary" as const
}
