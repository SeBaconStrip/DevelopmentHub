import vscodeIconUrl from "../assets/icons/vscode.svg";
import visualStudioIconUrl from "../assets/icons/visualstudio.svg";
import type { RepositoryOpener } from "../types";

export function OpenerIcon({ opener, size }: { opener: RepositoryOpener; size: number }) {
  if (opener.iconType === "vscode")
    return <img src={vscodeIconUrl} width={size} height={size} alt={opener.label} draggable={false} />;
  if (opener.iconType === "visualstudio")
    return <img src={visualStudioIconUrl} width={size} height={size} alt={opener.label} draggable={false} />;
  if (opener.iconPath)
    return <img src={`/api/icon-extractor?path=${encodeURIComponent(opener.iconPath)}`} width={size} height={size} alt={opener.label} draggable={false} />;
  return (
    <span className="opener-icon-initial" style={{ width: size, height: size, fontSize: size * 0.6 }}>
      {opener.label ? opener.label[0].toUpperCase() : "?"}
    </span>
  );
}
