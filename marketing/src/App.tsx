import { BrowserRouter } from "react-router-dom";
import AppProviders, { createAppQueryClient } from "./AppProviders";
import AppRoutes from "./AppRoutes";

const queryClient = createAppQueryClient();

const App = () => (
  <AppProviders queryClient={queryClient}>
    <BrowserRouter>
      <AppRoutes />
    </BrowserRouter>
  </AppProviders>
);

export default App;
