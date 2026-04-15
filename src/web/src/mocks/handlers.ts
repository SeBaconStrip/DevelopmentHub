import { http, HttpResponse } from 'msw';
import {
  mockRepositories,
  mockPullRequests,
  mockTodos,
  mockWorkflowDefinitions,
  mockWorkflowExecutions,
  mockConfig,
} from './data';

export const handlers = [
  http.get('/api/repositories', () => HttpResponse.json(mockRepositories)),

  http.get('/api/pullrequests', () => HttpResponse.json(mockPullRequests)),

  http.get('/api/todos', () => HttpResponse.json(mockTodos)),

  http.get('/api/workflows', () => HttpResponse.json(mockWorkflowDefinitions)),

  http.get('/api/workflows/executions', () => HttpResponse.json(mockWorkflowExecutions)),

  http.get('/api/config', () => HttpResponse.json(mockConfig)),

  // Mutations — return success without side effects
  http.post('/api/repositories/scan', () => new HttpResponse(null, { status: 202 })),

  http.post('/api/todos', async ({ request }) => {
    const body = await request.json() as { title: string; linkUrl?: string };
    return HttpResponse.json({
      id: crypto.randomUUID(),
      title: body.title,
      linkUrl: body.linkUrl ?? null,
      completed: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
      isSynced: false,
    });
  }),

  http.post('/api/launcher/open-url', () => new HttpResponse(null, { status: 204 })),
  http.post('/api/launcher/open-explorer', () => new HttpResponse(null, { status: 204 })),
];
