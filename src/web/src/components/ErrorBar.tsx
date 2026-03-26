interface ErrorBarProps {
  message: string | null;
  onDismiss: () => void;
  className?: string;
}

export function ErrorBar({ message, onDismiss, className }: ErrorBarProps) {
  if (!message) return null;
  return (
    <div className={`panel-error-bar${className ? ` ${className}` : ""}`}>
      <span>⚠ {message}</span>
      <button onClick={onDismiss}>✕</button>
    </div>
  );
}
