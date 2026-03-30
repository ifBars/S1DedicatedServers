import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const surfaceVariants = cva(
  "rounded-md border border-border bg-card text-card-foreground",
  {
    variants: {
      variant: {
        default: "",
        subtle: "bg-card/80",
        inset: "bg-background/40",
        raised: "shadow-sm",
      },
      padding: {
        none: "",
        sm: "p-3",
        md: "p-4",
      },
    },
    defaultVariants: {
      variant: "default",
      padding: "md",
    },
  }
)

function Surface({
  className,
  variant,
  padding,
  ...props
}: React.ComponentProps<"div"> & VariantProps<typeof surfaceVariants>) {
  return (
    <div
      data-slot="surface"
      className={cn(surfaceVariants({ variant, padding }), className)}
      {...props}
    />
  )
}

export { Surface, surfaceVariants }
