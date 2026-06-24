/**
 * DevelopmentHub Plugin UI Framework
 *
 * Pre-built React components that automatically follow the host's active theme.
 * Exposed to plugins via window.__dhSdk.ui — never import this file directly
 * from a plugin; consume it through the SDK object instead.
 */

import React from 'react';
import './ui.css';

type ButtonVariant = 'primary' | 'secondary' | 'ghost';

export function Button({
  variant = 'ghost',
  className,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant }) {
  return (
    <button
      className={['dh-btn', `dh-btn-${variant}`, className].filter(Boolean).join(' ')}
      {...props}
    />
  );
}

export function Card({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={['dh-card', className].filter(Boolean).join(' ')} {...props} />;
}

export function Input({ className, ...props }: React.InputHTMLAttributes<HTMLInputElement>) {
  return <input className={['dh-input', className].filter(Boolean).join(' ')} {...props} />;
}

type SearchInputProps = React.InputHTMLAttributes<HTMLInputElement> & {
  wrapperClassName?: string;
  onClear?: () => void;
};

export function SearchInput({
  value,
  onChange,
  className,
  wrapperClassName,
  onClear,
  ...props
}: SearchInputProps) {
  const handleClear = () => {
    if (onClear) {
      onClear();
    } else {
      onChange?.({ target: { value: '' } } as React.ChangeEvent<HTMLInputElement>);
    }
  };

  return (
    <div className={['dh-search-wrap', wrapperClassName].filter(Boolean).join(' ')}>
      <input
        className={['dh-input', 'dh-search-input', className].filter(Boolean).join(' ')}
        value={value}
        onChange={onChange}
        {...props}
      />
      {value && (
        <button type="button" className="dh-search-clear" onClick={handleClear} aria-label="Clear search">
          ✕
        </button>
      )}
    </div>
  );
}

export function Chip({
  color,
  style,
  className,
  ...props
}: React.HTMLAttributes<HTMLSpanElement> & { color?: string }) {
  return (
    <span
      className={['dh-chip', className].filter(Boolean).join(' ')}
      style={color ? { background: color, ...style } : style}
      {...props}
    />
  );
}

export function Empty({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={['dh-empty', className].filter(Boolean).join(' ')} {...props} />;
}

export function PageRoot({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={['dh-page', className].filter(Boolean).join(' ')} {...props} />;
}

export function Spinner({ size = 20 }: { size?: number }) {
  return (
    <span
      className="dh-spinner"
      style={{ width: size, height: size }}
      role="status"
      aria-label="Loading"
    />
  );
}
