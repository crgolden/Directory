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

  it('getChat GETs the correct URL and returns a chat', async () => {
    const promise = firstValueFrom(service.getChat('chat-123'));
    const req = httpMock.expectOne('/manuals/api/chats/chat-123');
    expect(req.request.method).toBe('GET');
    req.flush({ chatId: 'chat-123', title: 'My Chat', createdAt: 1700000000 });
    const result = await promise;
    expect(result.chatId).toBe('chat-123');
    expect(result.title).toBe('My Chat');
  });

  it('createChat POSTs to /manuals/api/chats and returns created chat', async () => {
    const promise = firstValueFrom(service.createChat());
    const req = httpMock.expectOne('/manuals/api/chats');
    expect(req.request.method).toBe('POST');
    req.flush({ chatId: 'chat-new', title: null, createdAt: 1700000000 });
    const result = await promise;
    expect(result.chatId).toBe('chat-new');
    expect(result.title).toBeNull();
  });

  it('updateChatTitle PATCHes the correct URL with merge-patch content type', async () => {
    const promise = firstValueFrom(service.updateChatTitle('chat-123', 'New Title'));
    const req = httpMock.expectOne('/manuals/api/chats/chat-123');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.headers.get('Content-Type')).toContain('application/merge-patch+json');
    expect(req.request.body).toEqual({ title: 'New Title' });
    req.flush(null, { status: 204, statusText: 'No Content' });
    await promise;
  });

  it('deleteChat DELETEs the correct URL', async () => {
    const promise = firstValueFrom(service.deleteChat('chat-123'));
    const req = httpMock.expectOne('/manuals/api/chats/chat-123');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    await promise;
  });

  it('getChatMessages GETs the correct URL and returns messages', async () => {
    const promise = firstValueFrom(service.getChatMessages('chat-123'));
    const req = httpMock.expectOne('/manuals/api/chats/chat-123/messages');
    expect(req.request.method).toBe('GET');
    req.flush([
      { role: 'user', text: 'Hello' },
      { role: 'assistant', text: 'Hi there!' },
    ]);
    const messages = await promise;
    expect(messages).toHaveLength(2);
    expect(messages[0].role).toBe('user');
    expect(messages[1].text).toBe('Hi there!');
  });

  it('sendMessage POSTs to the correct URL with input body', async () => {
    const promise = firstValueFrom(service.sendMessage('chat-123', 'Hello'));
    const req = httpMock.expectOne('/manuals/api/chats/chat-123/messages');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ input: 'Hello' });
    req.flush({ output: 'Hi there', chatId: 'chat-123' });
    const response = await promise;
    expect(response.output).toBe('Hi there');
    expect(response.chatId).toBe('chat-123');
  });

  it('streamMessage parses SSE deltas and completes on [DONE]', async () => {
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
      service.streamMessage('chat-123', 'Hi').subscribe({
        next: d => deltas.push(d),
        complete: resolve,
        error: reject,
      });
    });

    expect(deltas).toEqual(['Hello', ' world']);
    expect(fetchSpy).toHaveBeenCalledWith(
      '/manuals/api/chats/chat-123/messages/stream',
      expect.objectContaining({ method: 'POST' })
    );
    fetchSpy.mockRestore();
  });
});
