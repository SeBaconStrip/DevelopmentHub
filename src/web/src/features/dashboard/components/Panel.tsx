import "./Panel.css";

export function Panel({
  icon,
  title,
  badge,
  headerActions,
  isEditMode,
  onClose,
  onTitleClick,
  children,
}: {
  icon: string;
  title: string;
  badge?: number;
  headerActions?: React.ReactNode;
  isEditMode: boolean;
  onClose: () => void;
  onTitleClick?: () => void;
  children: React.ReactNode;
}) {
  return (
    <div className={`panel${isEditMode ? " panel--editing" : ""}`}>
      <div className="panel-header">
        <span className="panel-icon">{icon}</span>
        <span
          className={`panel-title${onTitleClick ? " panel-title--link" : ""}`}
          onClick={!isEditMode ? onTitleClick : undefined}
        >{title}</span>
        {badge != null && <span className="panel-badge">{badge}</span>}
        {headerActions && !isEditMode && (
          <div
            className="panel-header-actions"
            onPointerDown={(e) => e.stopPropagation()}
          >
            {headerActions}
          </div>
        )}
        {isEditMode && (
          <button
            className="panel-close-btn"
            onPointerDown={(e) => e.stopPropagation()}
            onClick={onClose}
          >
            ✕
          </button>
        )}
      </div>
      <div className="panel-body">{children}</div>
    </div>
  );
}
