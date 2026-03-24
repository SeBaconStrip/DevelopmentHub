# Examples

## Example 1 — Hello Plugin (bundled example)

The repository ships a complete example plugin at `plugins/example-plugin/`. It demonstrates:

- A dashboard widget with a click counter using `ui.Button`, `ui.Chip`, and `ui.Empty`
- A full page that calls the plugin's own backend API using `useQuery` and `ui.Input`
- A .NET backend with a single `GET /api/plugins/hello/greet?name=` endpoint

Explore the source:

```
plugins/example-plugin/
├── manifest.json
├── src/
│   ├── env.d.ts
│   ├── index.ts
│   ├── HelloWidget.tsx
│   └── HelloPage.tsx
└── backend/
    ├── ExamplePlugin.Backend.csproj
    ├── HelloPlugin.cs
    └── HelloController.cs
```

Build it:

```sh
# Frontend
cd plugins/example-plugin
npm install
npm run build

# Backend
dotnet build plugins/example-plugin/backend/ExamplePlugin.Backend.csproj
```

---

## Example 2 — Frontend-only widget with polling

A widget that fetches and auto-refreshes data from a host API endpoint every 30 seconds.

**`manifest.json` (relevant section)**
```json
"contributes": {
  "widgets": [
    {
      "id": "com.yourname.status-widget.status",
      "label": "System Status",
      "icon": "🟢",
      "defaultLayout": { "w": 4, "h": 5 }
    }
  ],
  "routes": []
}
```

**`src/StatusWidget.tsx`**
```tsx
const { React, useQuery, apiBase, ui } = window.__dhSdk;
const { Chip, Empty, Spinner } = ui;

interface StatusItem { name: string; healthy: boolean; }

export default function StatusWidget() {
  const { data, isLoading } = useQuery<StatusItem[]>({
    queryKey: ['com.yourname.status-widget', 'status'],
    queryFn: () => fetch(`${apiBase}/status`).then(r => r.json()),
    refetchInterval: 30_000,
  });

  if (isLoading) return <Spinner />;
  if (!data?.length) return <Empty>No services configured.</Empty>;

  return (
    <div style={{ padding: '0.75rem', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
      {data.map(item => (
        <div key={item.name} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontSize: 13, color: 'var(--text-primary)' }}>{item.name}</span>
          <Chip color={item.healthy ? 'var(--color-success)' : 'var(--color-error)'}
                style={{ color: '#fff' }}>
            {item.healthy ? 'OK' : 'DOWN'}
          </Chip>
        </div>
      ))}
    </div>
  );
}
```

**`src/index.ts`**
```ts
import StatusWidget from './StatusWidget';
const { plugin } = window.__dhSdk;
plugin.registerWidget('com.yourname.status-widget.status', StatusWidget);
```

---

## Example 3 — Plugin-local state with Zustand

A widget that maintains client-side state across re-renders without calling any API.

```tsx
const { React, createStore, ui } = window.__dhSdk;
const { Button, Chip } = ui;

interface TodoStore {
  items: string[];
  add: (item: string) => void;
  remove: (index: number) => void;
}

// Create the store once at module level
const useTodos = createStore<TodoStore>((set) => ({
  items: [],
  add: (item) => set((s) => ({ items: [...s.items, item] })),
  remove: (i) => set((s) => ({ items: s.items.filter((_, idx) => idx !== i) })),
}));

export default function TodoWidget() {
  const { useState } = React;
  const { items, add, remove } = useTodos();
  const [text, setText] = useState('');

  function submit() {
    if (text.trim()) { add(text.trim()); setText(''); }
  }

  return (
    <div style={{ padding: '1rem', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
      <div style={{ display: 'flex', gap: '0.5rem' }}>
        <input
          className="dh-input"
          value={text}
          onChange={e => setText(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && submit()}
          placeholder="New item…"
        />
        <Button variant="primary" onClick={submit}>Add</Button>
      </div>
      {items.map((item, i) => (
        <div key={i} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Chip>{item}</Chip>
          <Button variant="ghost" onClick={() => remove(i)} style={{ padding: '2px 8px', fontSize: 11 }}>
            ✕
          </Button>
        </div>
      ))}
    </div>
  );
}
```

---

## Example 4 — Backend with a service and LiteDB

A plugin that stores data in its own database file, separate from the host.

**`backend/MyDataService.cs`**
```csharp
using LiteDB;

namespace MyPlugin;

public interface IMyDataService
{
    IEnumerable<NoteDao> GetAll();
    void Add(string text);
    void Delete(int id);
}

public record NoteDao
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class MyDataService : IMyDataService, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<NoteDao> _notes;

    public MyDataService(IConfiguration configuration)
    {
        var dataDir = configuration["AppSettings:DataDirectory"] ?? ".";
        _db = new LiteDatabase(Path.Combine(dataDir, "my-plugin.db"));
        _notes = _db.GetCollection<NoteDao>("notes");
    }

    public IEnumerable<NoteDao> GetAll() => _notes.FindAll();

    public void Add(string text) =>
        _notes.Insert(new NoteDao { Text = text, CreatedAt = DateTime.UtcNow });

    public void Delete(int id) => _notes.Delete(id);

    public void Dispose() => _db.Dispose();
}
```

**`backend/MyPlugin.cs`** — register the service as singleton
```csharp
public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddControllers().AddApplicationPart(typeof(MyPlugin).Assembly);
    services.AddSingleton<IMyDataService, MyDataService>();
}
```

Note: `LiteDB` is a third-party package. Add it to the `.csproj`:
```xml
<PackageReference Include="LiteDB" Version="5.0.21" />
```
LiteDB is not part of the host, so it will be copied into `backend-dist/` and loaded from there.

---

## Example 5 — Navigation between plugin pages

A plugin with two pages and a link between them.

**`manifest.json`**
```json
"routes": [
  { "path": "/plugins/catalog",        "navLabel": "Catalog", "navOrder": 100 },
  { "path": "/plugins/catalog/detail", "navLabel": "",        "navOrder": 999 }
]
```

Setting `navLabel` to `""` hides a route from the sidebar while still making it accessible.

**`src/CatalogPage.tsx`**
```tsx
const { React, Link, ui } = window.__dhSdk;
const { PageRoot, Card, Chip } = ui;

const items = [{ id: 1, name: 'Widget A' }, { id: 2, name: 'Widget B' }];

export default function CatalogPage() {
  return (
    <PageRoot>
      <Card style={{ padding: '1.5rem' }}>
        <h2 style={{ margin: '0 0 1rem' }}>Catalog</h2>
        {items.map(item => (
          <div key={item.id} style={{ marginBottom: '0.5rem' }}>
            <Link to={`/plugins/catalog/detail?id=${item.id}`}
                  style={{ color: 'var(--color-primary)' }}>
              {item.name}
            </Link>
          </div>
        ))}
      </Card>
    </PageRoot>
  );
}
```

**`src/DetailPage.tsx`**
```tsx
const { React, useNavigate, ui } = window.__dhSdk;
const { PageRoot, Card, Button } = ui;

export default function DetailPage() {
  const navigate = useNavigate();
  const id = new URLSearchParams(window.location.search).get('id');

  return (
    <PageRoot>
      <Card style={{ maxWidth: 480, margin: '2rem auto', padding: '2rem' }}>
        <Button variant="ghost" onClick={() => navigate('/plugins/catalog')}
                style={{ marginBottom: '1rem' }}>
          ← Back
        </Button>
        <h2 style={{ margin: 0 }}>Item {id}</h2>
      </Card>
    </PageRoot>
  );
}
```

**`src/index.ts`**
```ts
import CatalogPage from './CatalogPage';
import DetailPage from './DetailPage';

const { plugin } = window.__dhSdk;
plugin.registerRoute('/plugins/catalog', CatalogPage);
plugin.registerRoute('/plugins/catalog/detail', DetailPage);
```
