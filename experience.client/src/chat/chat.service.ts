import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { ChatMessage, ChatResponse, ConversationDetails, ConversationItemSummary } from './chat.model';

@Injectable({ providedIn: 'root' })
export class ChatService {

  private readonly http = inject(HttpClient);

  getConversations(): Observable<string[]> {
    return this.http.get<string[]>('/manuals/api/chat/conversations');
  }

  createConversation(): Observable<string> {
    return this.http
      .post<{ conversationId: string }>('/manuals/api/chat/conversations', {})
      .pipe(map(r => r.conversationId));
  }

  sendMessage(input: string, conversationId: string): Observable<ChatResponse> {
    return this.http.post<ChatResponse>('/manuals/api/chat', { input, conversationId });
  }

  streamMessage(input: string, conversationId: string): Observable<string> {
    return new Observable<string>(subscriber => {
      const controller = new AbortController();

      fetch('/manuals/api/chat/stream', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF': '1',
        },
        credentials: 'include',
        body: JSON.stringify({ input, conversationId }),
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

  getConversation(conversationId: string): Observable<ConversationDetails> {
    return this.http.get<ConversationDetails>(`/manuals/api/chat/conversations/${conversationId}`);
  }

  getConversationItems(conversationId: string): Observable<ConversationItemSummary[]> {
    return this.http.get<ConversationItemSummary[]>(`/manuals/api/chat/conversations/${conversationId}/items`);
  }

  deleteConversation(conversationId: string): Observable<void> {
    return this.http.delete<void>(`/manuals/api/chat/conversations/${conversationId}`);
  }

  buildMessages(messages: ChatMessage[]): ChatMessage[] {
    return messages;
  }
}
