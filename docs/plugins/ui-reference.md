# UI Component Reference

All components are available via `window.__dhSdk.ui`. They use CSS custom properties internally, so they automatically follow whichever theme the user has selected.

```ts
const { ui } = window.__dhSdk;
const { Button, Card, Input, Chip, Empty, PageRoot, Spinner } = ui;
```

---

## `PageRoot`

Scrollable wrapper for full pages. Use as the outermost element of every page component.

```tsx
<PageRoot>
  {/* page content */}
</PageRoot>
```

**Props**: all standard `<div>` HTML attributes.

---

## `Card`

A themed surface card with rounded corners and a soft shadow.

```tsx
<Card style={{ maxWidth: 480, margin: '2rem auto', padding: '2rem' }}>
  <h2>Title</h2>
  <p>Content</p>
</Card>
```

**Props**: all standard `<div>` HTML attributes.

---

## `Button`

A themed button with three visual variants.

```tsx
<Button variant="primary" onClick={handleClick}>
  Save
</Button>

<Button variant="secondary">
  Cancel
</Button>

<Button variant="ghost" disabled>
  Disabled
</Button>
```

**Props**:

| Prop | Type | Default | Description |
|---|---|---|---|
| `variant` | `'primary' \| 'secondary' \| 'ghost'` | `'ghost'` | Visual style. |
| ...rest | `ButtonHTMLAttributes` | — | All standard `<button>` attributes (`onClick`, `disabled`, `type`, etc.) |

**Variants**:

- `primary` — filled with `--color-primary`, white label. Use for the main call to action.
- `secondary` — muted background with an accent-colored border. Use for secondary actions.
- `ghost` — transparent background with primary-colored label. Use for low-emphasis actions.

---

## `Input`

A themed single-line text input that stretches to its container width.

```tsx
<Input
  placeholder="Search…"
  value={query}
  onChange={(e) => setQuery(e.target.value)}
  onKeyDown={(e) => e.key === 'Enter' && handleSubmit()}
/>
```

**Props**: all standard `<input>` HTML attributes (`value`, `onChange`, `placeholder`, `type`, `disabled`, etc.).

---

## `Chip`

A small inline badge, useful for status labels, counts, or tags.

```tsx
<Chip>Draft</Chip>
<Chip color="#16a34a">Active</Chip>
```

**Props**:

| Prop | Type | Default | Description |
|---|---|---|---|
| `color` | string | `var(--branch-bg)` | Override the background color (any CSS color value). |
| ...rest | `HTMLAttributes<HTMLSpanElement>` | — | Standard span attributes. |

The text color defaults to `var(--branch-fg)`. When using a custom `color`, you may also want to set `style={{ color: '...' }}` for contrast.

---

## `Empty`

A centered, muted placeholder for empty lists or zero-data states.

```tsx
{items.length === 0 && <Empty>No items found.</Empty>}
```

**Props**: all standard `<div>` HTML attributes.

---

## `Spinner`

An animated circular loading indicator.

```tsx
{isLoading && <Spinner />}
{isLoading && <Spinner size={32} />}
```

**Props**:

| Prop | Type | Default | Description |
|---|---|---|---|
| `size` | number | `20` | Diameter in pixels. |

The spinner color follows `--color-primary`.

---

## Composing components

```tsx
const { React, ui, useQuery, apiBase } = window.__dhSdk;
const { useState } = React;
const { PageRoot, Card, Input, Button, Chip, Empty, Spinner } = ui;

export default function MyPage() {
  const [search, setSearch] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['items', search],
    queryFn: () =>
      fetch(`${apiBase}/plugins/my-plugin/items?q=${search}`)
        .then(r => r.json()),
  });

  return (
    <PageRoot>
      <Card style={{ padding: '1.5rem' }}>
        <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem' }}>
          <Input
            placeholder="Search items…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <Button variant="primary">Search</Button>
        </div>

        {isLoading && <Spinner />}

        {!isLoading && data?.length === 0 && (
          <Empty>No items match your search.</Empty>
        )}

        {!isLoading && data?.map((item: { id: string; name: string; status: string }) => (
          <div key={item.id} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.5rem 0' }}>
            <span style={{ color: 'var(--text-primary)' }}>{item.name}</span>
            <Chip>{item.status}</Chip>
          </div>
        ))}
      </Card>
    </PageRoot>
  );
}
```

---

## Using CSS variables for custom styling

When the built-in components are not enough, use CSS custom properties directly in `style` props. They always reflect the active theme.

```tsx
<div style={{
  background: 'var(--surface-muted)',
  border: '1px solid var(--border)',
  borderRadius: 8,
  padding: '0.75rem 1rem',
  color: 'var(--text-body)',
  fontSize: 13,
}}>
  Custom styled element
</div>
```

See the full token list in [Frontend Development → Using CSS variables directly](./frontend.md#using-css-variables-directly).
