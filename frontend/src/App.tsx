import { BrowserRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { useEffect } from "react";
import DashboardPage from "./features/dashboard/DashboardPage";
import { useUiStore } from "./store/uiStore";

const queryClient = new QueryClient();

function ThemeApplier() {
  const theme = useUiStore((s) => s.theme);
  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
  }, [theme]);
  return null;
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeApplier />
      <BrowserRouter>
        <div style={{ display: "flex", minHeight: "100vh" }}>
          <main style={{ flex: 1, overflow: "auto", minHeight: "100vh" }}>
            <Routes>
              <Route path="/*" element={<DashboardPage />} />
            </Routes>
          </main>
        </div>
      </BrowserRouter>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}
