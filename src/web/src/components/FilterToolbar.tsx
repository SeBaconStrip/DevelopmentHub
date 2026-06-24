import React from "react";
import "./FilterToolbar.css";

interface FilterOption {
  value: string;
  label: string;
}

interface FilterToolbarProps {
  search: string;
  onSearchChange: (value: string) => void;
  filter: string;
  onFilterChange: (value: string) => void;
  filters: FilterOption[];
  searchPlaceholder?: string;
  shownCount: number;
  totalCount: number;
  className?: string;
  children?: React.ReactNode;
}

export function FilterToolbar({
  search,
  onSearchChange,
  filter,
  onFilterChange,
  filters,
  searchPlaceholder = "Search…",
  shownCount,
  totalCount,
  className,
  children,
}: FilterToolbarProps) {
  return (
    <div className={`filter-toolbar${className ? ` ${className}` : ""}`}>
      <div className="filter-toolbar__left">
        <div className="filter-toolbar__search-wrap">
          <input
            className="filter-toolbar__search"
            placeholder={searchPlaceholder}
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
          />
          {search && (
            <button
              type="button"
              className="filter-toolbar__search-clear"
              onClick={() => onSearchChange("")}
              aria-label="Clear search"
            >
              ✕
            </button>
          )}
        </div>
        <div className="filter-toolbar__filters">
          {filters.map((f) => (
            <button
              key={f.value}
              className={`filter-toolbar__btn${filter === f.value ? " filter-toolbar__btn--active" : ""}`}
              onClick={() => onFilterChange(f.value)}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>
      <div className="filter-toolbar__right">
        {shownCount < totalCount && (
          <span className="filter-toolbar__count">{shownCount} / {totalCount}</span>
        )}
        {children}
      </div>
    </div>
  );
}
