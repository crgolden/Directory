import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { ChatService } from './chat.service';
import { ChatMessage, ConversationItemSummary } from './chat.model';

@Component({
  selector: 'app-chat',
  imports: [FormsModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChatComponent implements OnInit, OnDestroy {

  private readonly titleService = inject(Title);
  private readonly chatService = inject(ChatService);
  private readonly route = inject(ActivatedRoute);

  readonly messages = signal<ChatMessage[]>([]);
  readonly input = signal('');
  readonly streaming = signal(false);
  readonly conversationId = signal<string | null>(null);
  readonly conversations = signal<string[]>([]);

  ngOnInit(): void {
    this.titleService.setTitle('Experience | Chat');
    const q = this.route.snapshot.queryParamMap.get('q');
    if (q) {
      this.input.set(q);
    }
    this.loadConversations();
    this.startConversation();
  }

  ngOnDestroy(): void {
    // Do not delete on destroy — conversations persist in Redis for the user.
  }

  private loadConversations(): void {
    this.chatService.getConversations().subscribe(ids => {
      this.conversations.set(ids);
    });
  }

  private startConversation(): void {
    this.chatService.createConversation().subscribe(id => {
      this.conversationId.set(id);
      this.conversations.update(list => (list.includes(id) ? list : [id, ...list]));
    });
  }

  selectConversation(id: string): void {
    if (id === this.conversationId() || this.streaming()) return;
    this.conversationId.set(id);
    this.messages.set([]);
    this.chatService.getConversationItems(id).subscribe(items => {
      this.messages.set(this.itemsToMessages(items));
    });
  }

  private itemsToMessages(items: ConversationItemSummary[]): ChatMessage[] {
    return items
      .filter(item => (item.role === 'user' || item.role === 'assistant') && item.text !== null)
      .map(item => ({ role: item.role as 'user' | 'assistant', content: item.text! }));
  }

  send(): void {
    const text = this.input().trim();
    const id = this.conversationId();
    if (!text || !id || this.streaming()) return;

    this.messages.update(msgs => [...msgs, { role: 'user', content: text }]);
    this.input.set('');
    this.streaming.set(true);

    this.messages.update(msgs => [...msgs, { role: 'assistant', content: '' }]);

    this.chatService.streamMessage(text, id).subscribe({
      next: delta => {
        this.messages.update(msgs => {
          const updated = [...msgs];
          const last = updated[updated.length - 1];
          updated[updated.length - 1] = { ...last, content: last.content + delta };
          return updated;
        });
      },
      complete: () => this.streaming.set(false),
      error: () => this.streaming.set(false),
    });
  }

  newConversation(): void {
    this.messages.set([]);
    this.conversationId.set(null);
    this.startConversation();
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  truncate(id: string): string {
    return id.length > 16 ? `${id.slice(0, 16)}…` : id;
  }
}
