import { BrowserRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import DashboardPage from "./features/dashboard/DashboardPage";
import RepositoriesPage from "./features/repositories/RepositoriesPage";
import PullRequestsPage from "./features/pullRequests/PullRequestsPage";
import SettingsPage from "./features/settings/SettingsPage";

const queryClient = new QueryClient();

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <div style={{ display: "flex", minHeight: "100vh" }}>
          <main style={{ flex: 1, overflow: "auto", minHeight: "100vh" }}>
            <Routes>
              <Route path="/" element={<DashboardPage />} />
              <Route path="/repositories" element={<RepositoriesPage />} />
              <Route path="/pull-requests" element={<PullRequestsPage />} />
              <Route path="/settings" element={<SettingsPage />} />
            </Routes>
          </main>
        </div>
      </BrowserRouter>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}
