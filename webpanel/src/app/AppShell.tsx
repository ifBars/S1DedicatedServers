import type { ReactNode } from "react"
import { useState } from "react"
import { Menu, Power } from "lucide-react"

import type { BootstrapPayload, Overview } from "@/lib/panel-api"
import { getRouteForPage, type PanelPageId } from "@/app/routes"
import { Sidebar } from "@/components/layout/Sidebar"
import { HeaderBar } from "@/components/layout/HeaderBar"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet"
export function AppShell({
  page,
  boot,
  overview,
  error,
  isBusy,
  onNavigate,
  onSaveWorld,
  onReloadConfig,
  onShutdown,
  children,
}: {
  page: PanelPageId
  boot: BootstrapPayload
  overview: Overview
  error: string | null
  isBusy: boolean
  onNavigate: (page: PanelPageId) => void
  onSaveWorld: () => void
  onReloadConfig: () => void
  onShutdown: () => void
  children: ReactNode
}) {
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const [shutdownOpen, setShutdownOpen] = useState(false)

  const route = getRouteForPage(page)

  return (
    <>
      <div className="min-h-svh">
        <div className="mx-auto grid min-h-svh grid-cols-1 lg:grid-cols-[240px_minmax(0,1fr)]">
          <aside className="hidden lg:block">
            <div className="sticky top-0 h-svh">
              <Sidebar
                boot={boot}
                onNavigate={onNavigate}
                overview={overview}
                page={page}
              />
            </div>
          </aside>

          <div className="min-w-0 p-4">
            <HeaderBar
              boot={boot}
              overview={overview}
              pageLabel={route.label}
              leftAddon={
                <Sheet onOpenChange={setSidebarOpen} open={sidebarOpen}>
                  <SheetTrigger asChild>
                    <Button size="icon" variant="outline">
                      <Menu />
                      <span className="sr-only">Open navigation</span>
                    </Button>
                  </SheetTrigger>
                  <SheetContent className="p-0" side="left">
                    <SheetHeader className="sr-only">
                      <SheetTitle>Navigation</SheetTitle>
                      <SheetDescription>
                        Server management pages for the integrated web panel.
                      </SheetDescription>
                    </SheetHeader>
                    <Sidebar
                      boot={boot}
                      onNavigate={(next) => {
                        onNavigate(next)
                        setSidebarOpen(false)
                      }}
                      overview={overview}
                      page={page}
                    />
                  </SheetContent>
                </Sheet>
              }
              isBusy={isBusy}
              onReloadConfig={onReloadConfig}
              onRequestShutdown={() => setShutdownOpen(true)}
              onSaveWorld={onSaveWorld}
            />

            {error ? (
              <div className="mt-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {error}
              </div>
            ) : null}

            <div className="mt-4">{children}</div>
          </div>
        </div>
      </div>

      <Dialog onOpenChange={setShutdownOpen} open={shutdownOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Shut down the dedicated server?</DialogTitle>
            <DialogDescription>
              This will stop the server process. Connected players will lose their current session.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button onClick={() => setShutdownOpen(false)} variant="outline">
              Cancel
            </Button>
            <Button
              disabled={isBusy}
              onClick={() => {
                setShutdownOpen(false)
                onShutdown()
              }}
              variant="destructive"
            >
              <Power data-icon="inline-start" />
              Confirm shutdown
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
