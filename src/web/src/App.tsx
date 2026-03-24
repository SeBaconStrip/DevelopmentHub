import { BrowserRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { useState, useEffect } from "react";
import { AppLayout } from "./components/AppLayout";
import DashboardPage from "./pages/dashboard/DashboardPage";
import RepositoriesPage from "./pages/repositories/RepositoriesPage";
import PullRequestsPage from "./pages/pull-requests/PullRequestsPage";
import TodosPage from "./pages/todos/TodosPage";
import WorkflowsPage from "./pages/workflows/WorkflowsPage";
import QuickLinksPage from "./pages/quick-links/QuickLinksPage";
import { initPluginLoader, loadAllPlugins } from "./plugins/PluginLoader";
import { pluginRegistry } from "./plugins/PluginRegistry";
import { PluginRoute } from "./plugins/PluginRoute";

const queryClient = new QueryClient();
initPluginLoader(queryClient);

export default function App() {
  const [pluginsLoaded, setPluginsLoaded] = useState(false);

  useEffect(() => {
    loadAllPlugins().then(() => setPluginsLoaded(true));
  }, []);

  const pluginRoutes = pluginsLoaded ? pluginRegistry.routes : [];
  const pluginNavLinks = pluginRoutes.map((r) => ({ path: r.path, label: r.navLabel }));

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppLayout pluginNavLinks={pluginNavLinks}>
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/repositories" element={<RepositoriesPage />} />
            <Route path="/pull-requests" element={<PullRequestsPage />} />
            <Route path="/todos" element={<TodosPage />} />
            <Route path="/workflows" element={<WorkflowsPage />} />
            <Route path="/quick-links" element={<QuickLinksPage />} />
            {pluginRoutes.map((r) => (
              <Route key={r.path} path={r.path} element={<PluginRoute path={r.path} />} />
            ))}
          </Routes>
        </AppLayout>
      </BrowserRouter>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}
