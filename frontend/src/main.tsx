import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import App from "./App.tsx";

// Apply stored theme synchronously before first paint to avoid flash
const VALID_THEMES = ["violet", "dark", "ocean", "orange", "nature"];
const storedTheme = localStorage.getItem("dh-theme");
document.documentElement.setAttribute(
  "data-theme",
  VALID_THEMES.includes(storedTheme!) ? storedTheme! : "violet",
);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
