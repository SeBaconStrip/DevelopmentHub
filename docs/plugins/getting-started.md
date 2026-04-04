# Getting Started

This guide walks through creating a plugin that adds a dashboard widget and a full page, with an optional .NET backend.

## Prerequisites

- Node.js 18+ and npm
- .NET 9 SDK (only for backend part)

## 1. Create the plugin directory

```
plugins/
└── my-plugin/
```

## 2. Create `manifest.json`

```json
{
  "id": "com.yourname.my-plugin",
  "version": "1.0.0",
  "name": "My Plugin",
  "description": "A short description.",
  "minHostVersion": "0.10.0",
  "frontend": {
    "bundle": "ui/index.js",
    "enabled": true,
    "sdkVersion": "1"
  },
  "settings": [
    {
      "key": "greeting",
      "label": "Greeting text",
      "type": "text",
      "defaultValue": "Hello"
    }
  ],
  "contributes": {
    "widgets": [
      {
        "id": "com.yourname.my-plugin.my-widget",
        "label": "My Widget",
        "icon": "🔌",
        "defaultLayout": { "w": 4, "h": 6 }
      }
    ],
    "routes": [
      {
        "path": "/plugins/my-plugin",
        "navLabel": "My Plugin",
        "navOrder": 100
      }
    ]
  }
}
```

The `settings[]` array is optional. Remove it if your plugin has no user-configurable options.

> **ID convention**: use reverse-domain notation — `com.yourname.plugin-name`. Widget IDs must be prefixed with the plugin ID.

## 3. Set up the frontend project

Inside the plugin directory create the following files.

**`package.json`**
```json
{
  "name": "my-plugin",
  "version": "1.0.0",
  "private": true,
  "scripts": {
    "build": "vite build",
    "dev": "vite build --watch"
  },
  "devDependencies": {
    "@developmenthub/plugin-sdk": "^1.0.0",
    "@types/react": "^19.0.0",
    "@tanstack/react-query": "^5.0.0",
    "react": "^19.0.0",
    "react-router-dom": "^7.0.0",
    "zustand": "^5.0.0",
    "@vitejs/plugin-react": "^4.0.0",
    "typescript": "^5.0.0",
    "vite": "^6.0.0"
  }
}
```

**`tsconfig.json`**
```json
{
  "compilerOptions": {
    "target": "ES2020",
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "jsx": "react",
    "strict": true,
    "skipLibCheck": true
  },
  "include": ["src"]
}
```

**`vite.config.ts`**
```ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  // Classic JSX runtime: compiles JSX to React.createElement(), which
  // uses the React instance provided by the host at runtime.
  plugins: [react({ jsxRuntime: 'classic' })],
  build: {
    lib: {
      entry: 'src/index.ts',
      formats: ['es'],
      fileName: () => 'index.js',
    },
    outDir: 'ui',
    emptyOutDir: true,
    rollupOptions: {
      // These are provided by the host — never bundle them.
      external: [
        'react',
        'react-dom',
        'react/jsx-runtime',
        '@tanstack/react-query',
        'zustand',
        'react-router-dom',
      ],
    },
  },
});
```

**`src/env.d.ts`**
```ts
/// <reference types="vite/client" />

import type { DhSdk } from '@developmenthub/plugin-sdk';

declare global {
  interface Window {
    __dhSdk: DhSdk;
  }
}
```

## 4. Write the components

**`src/MyWidget.tsx`**
```tsx
const { React, ui, useQuery, apiFetch, apiBase } = window.__dhSdk;
const { useState } = React;
const { Button, Empty } = ui;

const PLUGIN_ID = 'com.yourname.my-plugin';

export default function MyWidget() {
  const [count, setCount] = useState(0);

  // Read settings live — do not use window.__dhSdk.settings (bundle-load snapshot)
  const { data: settings = {} } = useQuery<Record<string, string>>({
    queryKey: [PLUGIN_ID, 'settings'],
    queryFn: () =>
      apiFetch(`${apiBase}/plugins/${encodeURIComponent(PLUGIN_ID)}/settings`)
        .then(r => r.json()),
  });
  const greeting = settings['greeting'] ?? 'Hello';

  return (
    <div style={{ padding: '1rem' }}>
      {count === 0
        ? <Empty>Nothing here yet.</Empty>
        : <p style={{ color: 'var(--text-body)' }}>{greeting}! Count: {count}</p>
      }
      <Button variant="primary" onClick={() => setCount(c => c + 1)}>
        Increment
      </Button>
    </div>
  );
}
```

**`src/MyPage.tsx`**
```tsx
const { React, ui } = window.__dhSdk;
const { PageRoot, Card } = ui;

export default function MyPage() {
  return (
    <PageRoot>
      <Card style={{ maxWidth: 600, margin: '2rem auto', padding: '2rem' }}>
        <h2>My Plugin Page</h2>
        <p style={{ color: 'var(--text-secondary)' }}>Hello from the plugin!</p>
      </Card>
    </PageRoot>
  );
}
```

**`src/index.ts`** — entry point that registers everything
```ts
import MyWidget from './MyWidget';
import MyPage from './MyPage';

const { plugin } = window.__dhSdk;

plugin.registerWidget('com.yourname.my-plugin.my-widget', MyWidget);
plugin.registerRoute('/plugins/my-plugin', MyPage);
```

## 5. Install and build

```sh
npm install
npm run build
```

The output is `ui/index.js`.

## 6. Add an optional backend (skip if not needed)

Create `backend/MyPlugin.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>MyPlugin</AssemblyName>
    <OutputPath>../backend-dist</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="DevelopmentHub.Plugins"
                      Version="1.0.0"
                      ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

Create `backend/MyPlugin.cs`:

```csharp
using DevelopmentHub.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyPlugin;

public class MyPlugin : IPlugin
{
    public string Id => "com.yourname.my-plugin";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers()
            .AddApplicationPart(typeof(MyPlugin).Assembly);
    }

    public void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes) { }
}
```

Add a controller at `backend/MyController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace MyPlugin.Controllers;

[ApiController]
[Route("api/plugins/my-plugin")]
public class MyController : ControllerBase
{
    [HttpGet("hello")]
    public IActionResult Hello() =>
        Ok(new { message = "Hello from the plugin backend!" });
}
```

Add the `backend` section to `manifest.json`:

```json
"backend": {
  "assembly": "backend-dist/MyPlugin.dll",
  "enabled": true
}
```

Build the backend:

```sh
dotnet build backend/MyPlugin.csproj
```

## 7. Restart the host

DevelopmentHub loads plugins at startup. Restart the application and your widget and nav link will appear.
