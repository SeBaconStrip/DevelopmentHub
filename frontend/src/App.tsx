import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { Sidebar } from './components/Sidebar';
import { DashboardPage } from './features/dashboard/DashboardPage';
import { RepositoriesPage } from './features/repositories/RepositoriesPage';
import { ScriptsPage } from './features/scripts/ScriptsPage';
import { PullRequestsPage } from './features/pullRequests/PullRequestsPage';
import { SettingsPage } from './features/settings/SettingsPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, staleTime: 30_000 },
  },
});

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <div className="flex min-h-screen bg-gray-50">
          <Sidebar />
          <main className="flex-1 overflow-auto">
            <Routes>
              <Route path="/" element={<DashboardPage />} />
              <Route path="/repositories" element={<RepositoriesPage />} />
              <Route path="/scripts" element={<ScriptsPage />} />
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

export default App;
