import { useState, useEffect, useRef, createContext, useContext, useMemo } from "react";
import { NavLink } from "react-router-dom";
import { DashboardSettingsModal } from "./DashboardSettingsModal";
import "./AppLayout.css";

interface HeaderActionsCtx {
  setHeaderActions: (actions: React.ReactNode) => void;
}

export const HeaderActionsContext = createContext<HeaderActionsCtx>({
  setHeaderActions: () => {},
});

export function useHeaderActions(actions: React.ReactNode, deps: React.DependencyList) {
  const { setHeaderActions } = useContext(HeaderActionsContext);
  useEffect(() => {
    setHeaderActions(actions);
    return () => setHeaderActions(null);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);
}

export function AppLayout({
  children,
  pluginNavLinks = [],
}: {
  children: React.ReactNode;
  pluginNavLinks?: Array<{ path: string; label: string }>;
}) {
  const [headerActions, setHeaderActions] = useState<React.ReactNode>(null);
  const [showSettings, setShowSettings] = useState(false);
  const ctx = useMemo(() => ({ setHeaderActions }), []);
  const [isMaximized, setIsMaximized] = useState(true);
  const dragStartPos = useRef<{ x: number; y: number } | null>(null);

  useEffect(() => {
    const handler = (e: Event) => setIsMaximized((e as CustomEvent).detail.maximized);
    window.addEventListener("windowstate", handler);
    return () => window.removeEventListener("windowstate", handler);
  }, []);

  useEffect(() => {
    function onMouseMove(e: MouseEvent) {
      if (!dragStartPos.current) return;
      const { x, y } = dragStartPos.current;
      if (Math.abs(e.clientX - x) > 4 || Math.abs(e.clientY - y) > 4) {
        dragStartPos.current = null;
        window.chrome?.webview?.postMessage("drag");
      }
    }
    function onMouseUp() {
      dragStartPos.current = null;
    }
    window.addEventListener("mousemove", onMouseMove);
    window.addEventListener("mouseup", onMouseUp);
    return () => {
      window.removeEventListener("mousemove", onMouseMove);
      window.removeEventListener("mouseup", onMouseUp);
    };
  }, []);

  function sendWindowMsg(msg: string) {
    window.chrome?.webview?.postMessage(msg);
  }

  function handleHeaderMouseDown(e: React.MouseEvent) {
    if (e.button !== 0) return;
    if ((e.target as HTMLElement).closest("button, a")) return;
    dragStartPos.current = { x: e.clientX, y: e.clientY };
  }

  function handleHeaderDblClick(e: React.MouseEvent) {
    if ((e.target as HTMLElement).closest("button, a")) return;
    sendWindowMsg("maximize");
  }

  return (
    <HeaderActionsContext.Provider value={ctx}>
    <div className="app-root">
      <header
        className="app-header"
        onMouseDown={handleHeaderMouseDown}
        onDoubleClick={handleHeaderDblClick}
      >
        <span className="app-brand">Development Hub</span>

        <nav className="app-nav">
          <NavLink
            to="/"
            end
            className={({ isActive }) => "app-nav-link" + (isActive ? " app-nav-link--active" : "")}
          >
            Dashboard
          </NavLink>
          <NavLink
            to="/repositories"
            className={({ isActive }) => "app-nav-link" + (isActive ? " app-nav-link--active" : "")}
          >
            Repositories
          </NavLink>
          <NavLink
            to="/pull-requests"
            className={({ isActive }) => "app-nav-link" + (isActive ? " app-nav-link--active" : "")}
          >
            Pull Requests
          </NavLink>
          <NavLink
            to="/todos"
            className={({ isActive }) => "app-nav-link" + (isActive ? " app-nav-link--active" : "")}
          >
            Todos
          </NavLink>
          <NavLink
            to="/workflows"
            className={({ isActive }) => "app-nav-link" + (isActive ? " app-nav-link--active" : "")}
          >
            Workflows
          </NavLink>
          <NavLink
            to="/quick-links"
            className={({ isActive }) => "app-nav-link" + (isActive ? " app-nav-link--active" : "")}
          >
            Quick Links
          </NavLink>
          {pluginNavLinks.map((link) => (
            <NavLink
              key={link.path}
              to={link.path}
              className={({ isActive }) => "app-nav-link" + (isActive ? " app-nav-link--active" : "")}
            >
              {link.label}
            </NavLink>
          ))}
        </nav>

        <div className="app-header-end">
          <div className="app-header-actions" onMouseDown={(e) => e.stopPropagation()}>
            {headerActions}
            <button className="btn-ghost" onClick={() => setShowSettings(true)}>
              ⚙ Settings
            </button>
          </div>
          <div className="wc-buttons">
            <button className="wc-btn wc-minimize" onClick={() => sendWindowMsg("minimize")} title="Minimize">
              <svg width="10" height="10" viewBox="0 0 10 10"><rect y="4.5" width="10" height="1" fill="currentColor" /></svg>
            </button>
            <button className="wc-btn wc-maximize" onClick={() => sendWindowMsg("maximize")} title={isMaximized ? "Restore" : "Maximize"}>
              {isMaximized
                ? <svg width="10" height="10" viewBox="0 0 10 10"><rect x="0" y="3" width="7" height="7" fill="none" stroke="currentColor" strokeWidth="1" /><polyline points="3,3 3,0 10,0 10,7 7,7" fill="none" stroke="currentColor" strokeWidth="1" /></svg>
                : <svg width="10" height="10" viewBox="0 0 10 10"><rect x="0" y="0" width="10" height="10" fill="none" stroke="currentColor" strokeWidth="1" /></svg>
              }
            </button>
            <button className="wc-btn wc-close" onClick={() => sendWindowMsg("close")} title="Close">
              <svg width="10" height="10" viewBox="0 0 10 10"><line x1="0" y1="0" x2="10" y2="10" stroke="currentColor" strokeWidth="1.5" /><line x1="10" y1="0" x2="0" y2="10" stroke="currentColor" strokeWidth="1.5" /></svg>
            </button>
          </div>
        </div>
      </header>

      <div className="app-scroll">
        {children}
      </div>

      {showSettings && <DashboardSettingsModal onClose={() => setShowSettings(false)} />}
    </div>
    </HeaderActionsContext.Provider>
  );
}
