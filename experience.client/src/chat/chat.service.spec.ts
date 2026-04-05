import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ChatService } from './chat.service';
import { firstValueFrom } from 'rxjs';

describe('ChatService', () => {
  let service: ChatService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ChatService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getConversations GETs /manuals/api/chat/conversations and returns id list', async () => {
    const promise = firstValueFrom(service.getConversations());
    const req = httpMock.expectOne('/manuals/api/chat/conversations');
    expect(req.request.method).toBe('GET');
    req.flush(['conv-1', 'conv-2']);
    expect(await promise).toEqual(['conv-1', 'conv-2']);
  });

  it('createConversation POSTs to /manuals/api/chat/conversations and returns id', async () => {
    const promise = firstValueFrom(service.createConversation());
    const req = httpMock.expectOne('/manuals/api/chat/conversations');
    expect(req.request.method).toBe('POST');
    req.flush({ conversationId: 'conv-123' });
    expect(await promise).toBe('conv-123');
  });

  it('sendMessage POSTs correct body', async () => {
    const promise = firstValueFrom(service.sendMessage('Hello', 'conv-123'));
    const req = httpMock.expectOne('/manuals/api/chat');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ input: 'Hello', conversationId: 'conv-123' });
    req.flush({ output: 'Hi there', conversationId: 'conv-123' });
    const response = await promise;
    expect(response.output).toBe('Hi there');
  });

  it('deleteConversation DELETEs the correct URL', async () => {
    const promise = firstValueFrom(service.deleteConversation('conv-123'));
    const req = httpMock.expectOne('/manuals/api/chat/conversations/conv-123');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    await promise;
  });

  it('getConversation GETs the correct URL and returns details', async () => {
    const promise = firstValueFrom(service.getConversation('conv-123'));
    const req = httpMock.expectOne('/manuals/api/chat/conversations/conv-123');
    expect(req.request.method).toBe('GET');
    req.flush({ conversationId: 'conv-123', createdAt: 1700000000 });
    const result = await promise;
    expect(result.conversationId).toBe('conv-123');
    expect(result.createdAt).toBe(1700000000);
  });

  it('getConversationItems GETs the correct URL and returns items', async () => {
    const promise = firstValueFrom(service.getConversationItems('conv-123'));
    const req = httpMock.expectOne('/manuals/api/chat/conversations/conv-123/items');
    expect(req.request.method).toBe('GET');
    req.flush([
      { id: 'item-1', role: 'user', text: 'Hello' },
      { id: 'item-2', role: 'assistant', text: 'Hi there!' },
    ]);
    const items = await promise;
    expect(items).toHaveLength(2);
    expect(items[0].role).toBe('user');
    expect(items[1].text).toBe('Hi there!');
  });

  it('streamMessage parses SSE deltas and completes on [DONE]', async () => {
    // Build a minimal ReadableStream that emits SSE lines then [DONE]
    const sseChunk = [
      'data: {"delta":{"content":"Hello"}}\n\n',
      'data: {"delta":{"content":" world"}}\n\n',
      'data: [DONE]\n\n',
    ].join('');

    const encoder = new TextEncoder();
    const encoded = encoder.encode(sseChunk);

    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(
        new ReadableStream({
          start(controller) {
            controller.enqueue(encoded);
            controller.close();
          },
        }),
        { status: 200, headers: { 'Content-Type': 'text/event-stream' } }
      )
    );

    const deltas: string[] = [];
    await new Promise<void>((resolve, reject) => {
      service.streamMessage('Hi', 'conv-123').subscribe({
        next: d => deltas.push(d),
        complete: resolve,
        error: reject,
      });
    });

    expect(deltas).toEqual(['Hello', ' world']);
    fetchSpy.mockRestore();
  });
});
