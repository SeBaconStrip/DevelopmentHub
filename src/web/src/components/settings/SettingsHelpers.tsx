import { useState } from "react";
import type { DashboardWidget } from "../../store/uiStore";

export function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="settings-section">
      <h3 className="settings-section-title">{title}</h3>
      {children}
    </div>
  );
}

export function InfoTooltip({ content }: { content: React.ReactNode }) {
  const [visible, setVisible] = useState(false);
  return (
    <span
      className="info-tooltip-wrapper"
      onMouseEnter={() => setVisible(true)}
      onMouseLeave={() => setVisible(false)}
    >
      <span className="info-tooltip-icon">ℹ</span>
      {visible && <div className="info-tooltip-box">{content}</div>}
    </span>
  );
}

export function Field({
  label,
  tooltip,
  children,
}: {
  label: string;
  tooltip?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <label className="settings-field">
      <span className="settings-field-label">
        {label}
        {tooltip && <InfoTooltip content={tooltip} />}
      </span>
      {children}
    </label>
  );
}

export function AddLink({
  onClick,
  children,
}: {
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button className="btn-add-link" onClick={onClick}>
      {children}
    </button>
  );
}

interface RowProps {
  widget: DashboardWidget;
  onToggle: () => void;
}

export function WidgetRow({ widget, onToggle }: RowProps) {
  return (
    <div
      className={`widget-row${widget.enabled ? " widget-row--enabled" : ""}`}
    >
      <span className="widget-row-icon">{widget.icon}</span>
      <span className="widget-row-label">{widget.label}</span>
      <button
        className={`toggle-btn${widget.enabled ? " toggle-btn--on" : ""}`}
        onClick={onToggle}
        title={widget.enabled ? "Disable" : "Enable"}
      >
        <span
          className={`toggle-knob${widget.enabled ? " toggle-knob--on" : ""}`}
        />
      </button>
    </div>
  );
}
