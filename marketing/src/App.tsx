import { BrowserRouter } from "react-router-dom";
import { useEffect } from "react";
import AppProviders, { createAppQueryClient } from "./AppProviders";
import AppRoutes from "./AppRoutes";
import { registerWebMcpTools } from "./lib/webmcp";

const queryClient = createAppQueryClient();

const App = () => {
  useEffect(() => {
    registerWebMcpTools();
  }, []);

  return (
    <AppProviders queryClient={queryClient}>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </AppProviders>
  );
};

export default App;
