import { useState } from "react";
import type { WorkflowDefinition } from "../../../types";

export function WorkflowInputModal({
  workflow,
  onClose,
  onSubmit,
}: {
  workflow: WorkflowDefinition;
  onClose: () => void;
  onSubmit: (inputs: Record<string, string>) => void;
}) {
  const [values, setValues] = useState<Record<string, string>>(
    Object.fromEntries(
      workflow.inputs.map((input) => [input.name, input.defaultValue ?? ""]),
    ),
  );

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal-card workflow-input-modal"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="workflow-modal-header">
          <div>
            <h2 className="settings-modal-title">{workflow.name}</h2>
            <p className="settings-modal-sub">Enter the workflow inputs.</p>
          </div>
          <button className="settings-modal-close" onClick={onClose}>
            ×
          </button>
        </div>
        <div className="workflow-input-fields">
          {workflow.inputs.map((input) => (
            <label key={input.name} className="settings-field">
              <span className="settings-field-label">
                {input.label || input.name}
              </span>
              <input
                className="settings-input"
                value={values[input.name] ?? ""}
                onChange={(e) =>
                  setValues((prev) => ({
                    ...prev,
                    [input.name]: e.target.value,
                  }))
                }
              />
            </label>
          ))}
        </div>
        <div className="settings-save-bar">
          <button className="btn-primary" onClick={() => onSubmit(values)}>
            Run workflow
          </button>
          <button className="btn-close" onClick={onClose}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
