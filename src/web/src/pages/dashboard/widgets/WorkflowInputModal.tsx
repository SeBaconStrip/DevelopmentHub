import { useState } from "react";
import type { WorkflowDefinition } from "../../../types";
import { Modal } from "../../../components/Modal";

export function WorkflowInputModal({
  workflow,
  onClose,
  onSubmit,
}: {
  workflow: WorkflowDefinition;
  onClose: () => void;
  onSubmit: (inputs: Record<string, string>, skippedSteps: string[]) => void;
}) {
  const [values, setValues] = useState<Record<string, string>>(
    Object.fromEntries(
      workflow.inputs.map((input) => [input.name, input.defaultValue ?? ""]),
    ),
  );
  const [enabledSteps, setEnabledSteps] = useState<Set<string>>(
    () => new Set(workflow.steps.map((s) => s.name)),
  );

  function toggleStep(name: string) {
    setEnabledSteps((prev) => {
      const next = new Set(prev);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  }

  const skippedSteps = workflow.steps.map((s) => s.name).filter((n) => !enabledSteps.has(n));

  return (
    <Modal
      onClose={onClose}
      title={workflow.name}
      subtitle={workflow.description || "Configure and run workflow."}
      className="workflow-input-modal"
    >
      {workflow.inputs.length > 0 && (
        <div className="workflow-input-fields">
          <span className="workflow-section-label">Inputs</span>
          {workflow.inputs.map((input) => (
            <label key={input.name} className="settings-field">
              <span className="settings-field-label">{input.label || input.name}</span>
              {input.type === "bool" ? (
                <input
                  type="checkbox"
                  className="workflow-input-checkbox"
                  checked={values[input.name] === "true"}
                  onChange={(e) =>
                    setValues((prev) => ({ ...prev, [input.name]: e.target.checked ? "true" : "false" }))
                  }
                />
              ) : input.type === "select" ? (
                <select
                  className="settings-input"
                  value={values[input.name] || input.options?.[0] || ""}
                  onChange={(e) =>
                    setValues((prev) => ({ ...prev, [input.name]: e.target.value }))
                  }
                >
                  {(input.options ?? []).map((opt) => (
                    <option key={opt} value={opt}>{opt}</option>
                  ))}
                </select>
              ) : (
                <input
                  className="settings-input"
                  value={values[input.name] ?? ""}
                  onChange={(e) =>
                    setValues((prev) => ({ ...prev, [input.name]: e.target.value }))
                  }
                />
              )}
            </label>
          ))}
        </div>
      )}

      <details className="workflow-steps-advanced">
        <summary className="workflow-steps-summary">
          Advanced{skippedSteps.length > 0 && <span className="workflow-steps-badge">{skippedSteps.length} skipped</span>}
        </summary>
        <div className="workflow-steps-section">
          <span className="workflow-section-label">Steps</span>
          {workflow.steps.map((step) => (
            <label key={step.name} className="workflow-step-row">
              <input
                type="checkbox"
                checked={enabledSteps.has(step.name)}
                onChange={() => toggleStep(step.name)}
              />
              <span className="workflow-step-name">{step.name}</span>
              <span className="workflow-step-type">{step.type}</span>
            </label>
          ))}
        </div>
      </details>

      <div className="settings-save-bar">
        <button className="btn-primary" onClick={() => onSubmit(values, skippedSteps)}>
          {workflow.runElevated ? "Run elevated" : "Run workflow"}
        </button>
        {workflow.runElevated && (
          <span className="workflow-elevated-badge" title="All steps that support UAC elevation will run elevated">elevated</span>
        )}
        <button className="btn-close" onClick={onClose}>
          Cancel
        </button>
      </div>
    </Modal>
  );
}
