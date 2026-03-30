import { Surface } from "@/components/panel/Surface"

export function EmptyState({
  title,
  description,
  details,
}: {
  title: string
  description: string
  details?: string
}) {
  return (
    <Surface padding="md" className="max-w-2xl">
      <h2 className="text-lg font-semibold text-foreground">{title}</h2>
      <p className="mt-2 text-sm text-muted-foreground">{description}</p>
      {details ? (
        <p className="mt-4 whitespace-pre-wrap text-sm text-muted-foreground">
          {details}
        </p>
      ) : null}
    </Surface>
  )
}
