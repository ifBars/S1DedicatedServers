import { Route, Routes } from "react-router-dom";
import ConfigGeneratorPage from "./features/config-generator/ConfigGeneratorPage";
import ServerPortalPage from "./features/server-portal/ServerPortalPage";
import Index from "./pages/Index";
import NotFound from "./pages/NotFound";

const AppRoutes = () => (
  <Routes>
    <Route path="/" element={<Index />} />
    <Route path="/config-generator" element={<ConfigGeneratorPage />} />
    <Route path="/server-portal" element={<ServerPortalPage />} />
    <Route path="*" element={<NotFound />} />
  </Routes>
);

export default AppRoutes;
