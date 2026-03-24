import { useState, useRef } from "react";

interface TagEditorProps {
  tags: string[];
  onSave: (tags: string[]) => void;
}

export function TagEditor({ tags, onSave }: TagEditorProps) {
  const [adding, setAdding] = useState(false);
  const [input, setInput] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);

  const commit = () => {
    const trimmed = input.trim();
    if (trimmed && !tags.includes(trimmed)) {
      onSave([...tags, trimmed]);
    }
    setInput("");
    setAdding(false);
  };

  const remove = (tag: string) => onSave(tags.filter((t) => t !== tag));

  return (
    <div className="tag-editor">
      {tags.map((tag) => (
        <span key={tag} className="tag-chip">
          {tag}
          <button className="tag-chip-remove" onClick={() => remove(tag)} title="Tag entfernen">×</button>
        </span>
      ))}
      {adding ? (
        <input
          ref={inputRef}
          className="tag-input"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") commit();
            if (e.key === "Escape") { setAdding(false); setInput(""); }
          }}
          onBlur={commit}
          autoFocus
          placeholder="Tag…"
          maxLength={32}
        />
      ) : (
        <button className="tag-add-btn" onClick={() => setAdding(true)} title="Tag hinzufügen">+</button>
      )}
    </div>
  );
}
