import { startTransition, useEffect, useState } from "react"

import { getPageFromPath, getPathForPage, type PanelPageId } from "./routes"

export function usePanelRouter() {
  const [page, setPage] = useState<PanelPageId>(() =>
    getPageFromPath(window.location.pathname)
  )

  useEffect(() => {
    const handlePopState = () => {
      startTransition(() => {
        setPage(getPageFromPath(window.location.pathname))
      })
    }

    window.addEventListener("popstate", handlePopState)
    return () => {
      window.removeEventListener("popstate", handlePopState)
    }
  }, [])

  const navigate = (nextPage: PanelPageId) => {
    const nextPath = getPathForPage(nextPage)
    if (window.location.pathname !== nextPath) {
      window.history.pushState({}, document.title, nextPath)
    }

    startTransition(() => {
      setPage(nextPage)
    })
  }

  return { page, navigate }
}
