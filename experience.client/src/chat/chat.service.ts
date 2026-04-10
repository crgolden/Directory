import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Chat, ChatHistoryMessage, ChatResponse } from './chat.model';

@Injectable({ providedIn: 'root' })
export class ChatService {

  private readonly http = inject(HttpClient);

  getChats(): Observable<Chat[]> {
    return this.http.get<Chat[]>('/manuals/api/chats');
  }

  getChat(chatId: string): Observable<Chat> {
    return this.http.get<Chat>(`/manuals/api/chats/${chatId}`);
  }

  createChat(): Observable<Chat> {
    return this.http.post<Chat>('/manuals/api/chats', {});
  }

  updateChatTitle(chatId: string, title: string): Observable<void> {
    return this.http.patch<void>(`/manuals/api/chats/${chatId}`, { title }, {
      headers: { 'Content-Type': 'application/merge-patch+json' },
    });
  }

  deleteChat(chatId: string): Observable<void> {
    return this.http.delete<void>(`/manuals/api/chats/${chatId}`);
  }

  getChatMessages(chatId: string): Observable<ChatHistoryMessage[]> {
    return this.http.get<ChatHistoryMessage[]>(`/manuals/api/chats/${chatId}/messages`);
  }

  sendMessage(chatId: string, input: string): Observable<ChatResponse> {
    return this.http.post<ChatResponse>(`/manuals/api/chats/${chatId}/messages`, { input });
  }

  streamMessage(chatId: string, input: string): Observable<string> {
    return new Observable<string>(subscriber => {
      const controller = new AbortController();

      fetch(`/manuals/api/chats/${chatId}/messages/stream`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF': '1',
        },
        credentials: 'include',
        body: JSON.stringify({ input }),
        signal: controller.signal,
      })
        .then(async response => {
          if (!response.ok) {
            subscriber.error(new Error(`HTTP ${response.status}`));
            return;
          }

          const reader = response.body!.getReader();
          const decoder = new TextDecoder();
          let buffer = '';

          while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split('\n');
            buffer = lines.pop() ?? '';

            for (const line of lines) {
              if (!line.startsWith('data: ')) continue;
              const data = line.slice(6).trim();
              if (data === '[DONE]') {
                subscriber.complete();
                return;
              }
              try {
                const parsed = JSON.parse(data) as { delta: { content: string } };
                subscriber.next(parsed.delta.content);
              } catch {
                // ignore malformed lines
              }
            }
          }

          subscriber.complete();
        })
        .catch(err => {
          if ((err as Error).name !== 'AbortError') {
            subscriber.error(err);
          }
        });

      return () => controller.abort();
    });
  }
}
