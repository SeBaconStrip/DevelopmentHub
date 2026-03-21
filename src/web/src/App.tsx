import { BrowserRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { AppLayout } from "./components/AppLayout";
import DashboardPage from "./features/dashboard/DashboardPage";
import RepositoriesPage from "./features/repositories/RepositoriesPage";
import PullRequestsPage from "./features/pull-requests/PullRequestsPage";
import TodosPage from "./features/todos/TodosPage";
import WorkflowsPage from "./features/workflows/WorkflowsPage";
import QuickLinksPage from "./features/quick-links/QuickLinksPage";

const queryClient = new QueryClient();

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppLayout>
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/repositories" element={<RepositoriesPage />} />
            <Route path="/pull-requests" element={<PullRequestsPage />} />
            <Route path="/todos" element={<TodosPage />} />
            <Route path="/workflows" element={<WorkflowsPage />} />
            <Route path="/quick-links" element={<QuickLinksPage />} />
          </Routes>
        </AppLayout>
      </BrowserRouter>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}
