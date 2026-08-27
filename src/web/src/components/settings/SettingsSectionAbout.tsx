import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import { Section } from "./SettingsHelpers";

interface VersionResponse {
  version: string;
}

export function SettingsSectionAbout() {
  const { data, isLoading, isError } = useQuery<VersionResponse>({
    queryKey: ["version"],
    queryFn: () => apiFetch("/api/version").then((r) => r.json()),
    staleTime: Infinity,
  });

  const version = isLoading ? "…" : isError ? "unknown" : (data?.version ?? "unknown");

  return (
    <>
      <Section title="About">
        <div className="about-rows">
          <div className="about-row">
            <span className="about-row-label">Application</span>
            <span className="about-row-value">DevelopmentHub</span>
          </div>
          <div className="about-row">
            <span className="about-row-label">Version</span>
            <span className="about-row-value about-row-value--mono">
              {version}
            </span>
          </div>
        </div>
        <p className="settings-page-hint" style={{ marginTop: 12 }}>
          Reported by the backend at startup.
        </p>
      </Section>
    </>
  );
}
