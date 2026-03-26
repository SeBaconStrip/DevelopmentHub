import React from "react";

interface ModalProps {
  onClose: () => void;
  title: string;
  subtitle?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}

export function Modal({ onClose, title, subtitle, children, className }: ModalProps) {
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className={`modal-card${className ? ` ${className}` : ""}`}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="workflow-modal-header">
          <div>
            <h2 className="settings-modal-title">{title}</h2>
            {subtitle && <p className="settings-modal-sub">{subtitle}</p>}
          </div>
          <button className="settings-modal-close" onClick={onClose}>
            ×
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}
