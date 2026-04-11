import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { ChatService } from './chat.service';
import { Chat, ChatHistoryMessage, ChatMessage } from './chat.model';
import { MarkdownPipe } from './markdown.pipe';

@Component({
  selector: 'app-chat',
  imports: [FormsModule, MarkdownPipe],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChatComponent implements OnInit {

  private readonly titleService = inject(Title);
  private readonly chatService = inject(ChatService);
  private readonly route = inject(ActivatedRoute);

  readonly messages = signal<ChatMessage[]>([]);
  readonly input = signal('');
  readonly streaming = signal(false);
  readonly chatId = signal<string | null>(null);
  readonly chats = signal<Chat[]>([]);
  readonly editingChatId = signal<string | null>(null);
  readonly editingTitle = signal('');

  ngOnInit(): void {
    this.titleService.setTitle('Experience | Chat');
    const q = this.route.snapshot.queryParamMap.get('q');
    if (q) {
      this.input.set(q);
    }
    this.loadChats();
  }

  private loadChats(): void {
    this.chatService.getChats().subscribe(chats => {
      this.chats.set(chats);
    });
  }

  selectChat(id: string): void {
    if (id === this.chatId() || this.streaming()) return;
    this.chatId.set(id);
    this.messages.set([]);
    this.chatService.getChatMessages(id).subscribe(items => {
      this.messages.set(this.itemsToMessages(items));
    });
  }

  private itemsToMessages(items: ChatHistoryMessage[]): ChatMessage[] {
    return items
      .filter(item => (item.role === 'user' || item.role === 'assistant') && item.text !== null)
      .map(item => ({ role: item.role as 'user' | 'assistant', content: item.text! }));
  }

  send(): void {
    const text = this.input().trim();
    const id = this.chatId();
    if (!text || !id || this.streaming()) return;

    this.messages.update(msgs => [...msgs, { role: 'user', content: text }]);
    this.input.set('');
    this.streaming.set(true);

    this.messages.update(msgs => [...msgs, { role: 'assistant', content: '' }]);

    this.chatService.streamMessage(id, text).subscribe({
      next: delta => {
        this.messages.update(msgs => {
          const updated = [...msgs];
          const last = updated.at(-1)!;
          updated[updated.length - 1] = { ...last, content: last.content + delta };
          return updated;
        });
      },
      complete: () => {
        this.streaming.set(false);
        this.refreshChatTitle(id);
      },
      error: () => this.streaming.set(false),
    });
  }

  newChat(): void {
    this.chatService.createChat().subscribe(chat => {
      this.chats.update(list => [chat, ...list]);
      this.chatId.set(chat.chatId);
      this.messages.set([]);
    });
  }

  deleteChat(chatId: string): void {
    this.chatService.deleteChat(chatId).subscribe(() => {
      this.chats.update(list => list.filter(c => c.chatId !== chatId));
      if (this.chatId() === chatId) {
        this.chatId.set(null);
        this.messages.set([]);
      }
    });
  }

  startEditing(chat: Chat, event: Event): void {
    event.stopPropagation();
    this.editingChatId.set(chat.chatId);
    this.editingTitle.set(chat.title ?? '');
    // Focus the input after Angular has rendered it.
    setTimeout(() => {
      const input = document.querySelector<HTMLInputElement>('.chat-title-input');
      input?.focus();
      input?.select();
    });
  }

  commitTitle(chatId: string): void {
    const title = this.editingTitle().trim();
    this.editingChatId.set(null);
    if (!title) return;
    const current = this.chats().find(c => c.chatId === chatId);
    if (!current || title === current.title) return;
    this.chatService.updateChatTitle(chatId, title).subscribe(() => {
      this.chats.update(list =>
        list.map(c => c.chatId === chatId ? { ...c, title } : c)
      );
    });
  }

  cancelEditing(): void {
    this.editingChatId.set(null);
  }

  onTitleKeydown(event: KeyboardEvent, chatId: string): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.commitTitle(chatId);
    } else if (event.key === 'Escape') {
      this.cancelEditing();
    }
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  chatLabel(chat: Chat): string {
    return chat.title ?? this.truncate(chat.chatId);
  }

  truncate(id: string): string {
    return id.length > 16 ? `${id.slice(0, 16)}…` : id;
  }

  private refreshChatTitle(chatId: string): void {
    this.chatService.getChat(chatId).subscribe(updated => {
      this.chats.update(list =>
        list.map(c => c.chatId === chatId ? updated : c)
      );
    });
  }
}
