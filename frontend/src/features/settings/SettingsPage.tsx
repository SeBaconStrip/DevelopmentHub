import { useState, useEffect } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { configApi } from '../../api/config';
import type { AppConfig, ScriptConfig } from '../../types';

export function SettingsPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['config'],
    queryFn: configApi.get,
  });

  const save = useMutation({
    mutationFn: configApi.save,
  });

  const [form, setForm] = useState<AppConfig | null>(null);

  useEffect(() => {
    if (data) setForm(JSON.parse(JSON.stringify(data)));
  }, [data]);

  if (isLoading || !form) return <p className="p-6 text-gray-500">Loading configuration…</p>;

  const setField = <K extends keyof AppConfig>(key: K, value: AppConfig[K]) =>
    setForm(prev => prev ? { ...prev, [key]: value } : prev);

  const setAzDO = <K extends keyof AppConfig['azureDevOps']>(key: K, value: string) =>
    setForm(prev => prev ? { ...prev, azureDevOps: { ...prev.azureDevOps, [key]: value } } : prev);

  const addRoot = () => setField('repositoryRoots', [...form.repositoryRoots, '']);
  const removeRoot = (i: number) => setField('repositoryRoots', form.repositoryRoots.filter((_, idx) => idx !== i));
  const updateRoot = (i: number, val: string) =>
    setField('repositoryRoots', form.repositoryRoots.map((r, idx) => idx === i ? val : r));

  const addScript = () =>
    setField('scripts', [...form.scripts, {
      id: crypto.randomUUID(),
      name: '',
      description: '',
      workingDirectory: '',
      command: '',
      arguments: [],
      environmentVariables: {},
    } satisfies ScriptConfig]);

  const removeScript = (i: number) =>
    setField('scripts', form.scripts.filter((_, idx) => idx !== i));

  const updateScript = <K extends keyof ScriptConfig>(i: number, key: K, value: ScriptConfig[K]) =>
    setField('scripts', form.scripts.map((s, idx) => idx === i ? { ...s, [key]: value } : s));

  return (
    <div className="p-6 flex flex-col gap-8 max-w-3xl">
      <h1 className="text-2xl font-bold text-gray-900">Settings</h1>

      {/* Repository Roots */}
      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-800">Repository Root Directories</h2>
        {form.repositoryRoots.map((root, i) => (
          <div key={i} className="flex gap-2">
            <input
              value={root}
              onChange={e => updateRoot(i, e.target.value)}
              placeholder="C:\Projects"
              className="flex-1 border border-gray-300 rounded-md px-3 py-1.5 text-sm font-mono"
            />
            <button onClick={() => removeRoot(i)} className="text-red-500 hover:text-red-700 px-2 text-lg">✕</button>
          </div>
        ))}
        <button onClick={addRoot} className="text-sm text-blue-600 hover:underline w-fit">+ Add directory</button>
      </section>

      {/* Azure DevOps */}
      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-800">Azure DevOps</h2>
        {([
          ['Organization', 'organization', 'myorg'],
          ['Project', 'project', 'MyProject'],
          ['User Email', 'userEmail', 'you@example.com'],
        ] as const).map(([label, key, placeholder]) => (
          <label key={key} className="flex flex-col gap-1">
            <span className="text-sm text-gray-600">{label}</span>
            <input
              value={form.azureDevOps[key]}
              onChange={e => setAzDO(key, e.target.value)}
              placeholder={placeholder}
              className="border border-gray-300 rounded-md px-3 py-1.5 text-sm"
            />
          </label>
        ))}
        <label className="flex flex-col gap-1">
          <span className="text-sm text-gray-600">Personal Access Token</span>
          <input
            type="password"
            value={form.azureDevOps.pat}
            onChange={e => setAzDO('pat', e.target.value)}
            placeholder="Leave blank to keep existing"
            className="border border-gray-300 rounded-md px-3 py-1.5 text-sm"
          />
          <span className="text-xs text-gray-400">Required scope: vso.code</span>
        </label>
      </section>

      {/* Scan interval */}
      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-800">Scan Settings</h2>
        <label className="flex flex-col gap-1">
          <span className="text-sm text-gray-600">Scan interval (minutes)</span>
          <input
            type="number"
            value={form.scanIntervalMinutes}
            onChange={e => setField('scanIntervalMinutes', Number(e.target.value))}
            className="border border-gray-300 rounded-md px-3 py-1.5 text-sm w-32"
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-sm text-gray-600">Entry point search depth</span>
          <input
            type="number"
            value={form.entryPointMaxDepth}
            onChange={e => setField('entryPointMaxDepth', Number(e.target.value))}
            className="border border-gray-300 rounded-md px-3 py-1.5 text-sm w-32"
          />
        </label>
      </section>

      {/* Scripts */}
      <section className="flex flex-col gap-4">
        <h2 className="text-base font-semibold text-gray-800">Scripts</h2>
        {form.scripts.map((script, i) => (
          <div key={script.id} className="border border-gray-200 rounded-lg p-4 flex flex-col gap-3">
            <div className="flex justify-between">
              <span className="text-sm font-medium text-gray-700">Script #{i + 1}</span>
              <button onClick={() => removeScript(i)} className="text-red-500 hover:text-red-700 text-sm">Remove</button>
            </div>
            {([
              ['Name', 'name', 'Reset Database'],
              ['Description', 'description', 'Drops and recreates the local DB'],
              ['Working Directory', 'workingDirectory', 'C:\\Projects\\MyApp'],
              ['Command', 'command', 'dotnet'],
            ] as const).map(([label, key, placeholder]) => (
              <label key={key} className="flex flex-col gap-1">
                <span className="text-xs text-gray-500">{label}</span>
                <input
                  value={script[key]}
                  onChange={e => updateScript(i, key, e.target.value)}
                  placeholder={placeholder}
                  className="border border-gray-300 rounded-md px-3 py-1.5 text-sm"
                />
              </label>
            ))}
            <label className="flex flex-col gap-1">
              <span className="text-xs text-gray-500">Arguments (one per line)</span>
              <textarea
                value={script.arguments.join('\n')}
                onChange={e => updateScript(i, 'arguments', e.target.value.split('\n').filter(Boolean))}
                placeholder="ef&#10;database&#10;drop"
                rows={3}
                className="border border-gray-300 rounded-md px-3 py-1.5 text-sm font-mono"
              />
            </label>
          </div>
        ))}
        <button onClick={addScript} className="text-sm text-blue-600 hover:underline w-fit">+ Add script</button>
      </section>

      <div className="flex items-center gap-3">
        <button
          onClick={() => save.mutate(form)}
          disabled={save.isPending}
          className="bg-blue-600 hover:bg-blue-700 text-white font-medium px-5 py-2 rounded-lg transition-colors disabled:opacity-50"
        >
          {save.isPending ? 'Saving…' : '💾 Save Configuration'}
        </button>
        {save.isSuccess && <span className="text-green-600 text-sm">✓ Saved</span>}
        {save.isError && <span className="text-red-500 text-sm">✗ Failed to save</span>}
      </div>
    </div>
  );
}
