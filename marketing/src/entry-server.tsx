import { renderToString } from "react-dom/server";
import { StaticRouter } from "react-router-dom/server";
import AppProviders, { createAppQueryClient } from "./AppProviders";
import AppRoutes from "./AppRoutes";
import "./index.css";

export function render(url: string) {
  const queryClient = createAppQueryClient();

  return renderToString(
    <AppProviders queryClient={queryClient}>
      <StaticRouter location={url}>
        <AppRoutes />
      </StaticRouter>
    </AppProviders>,
  );
}
