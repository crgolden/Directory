import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatService } from './chat.service';
import { ChatMessage, ProductContext } from './chat.model';
import { MarkdownPipe } from './markdown.pipe';

/**
 * Matches HTTP(S) URLs in assistant replies. Intentionally stops at whitespace
 * and closing punctuation so that markdown/paren-wrapped links still extract
 * cleanly.
 */
const URL_REGEX = /\bhttps?:\/\/[^\s)>\]"']+/g;

/**
 * Embedded single-chat variant of the Manuals chat feature. Lives inside
 * {@link ManualChatPanelComponent} and is only instantiated when the user
 * opens the panel from the product create/edit form.
 *
 * Differences from the former standalone ChatComponent:
 *  - No multi-chat sidebar — this component owns exactly one chat for the
 *    lifetime of the product form session.
 *  - On the first user message, creates the chat in the Manuals API and
 *    sets its title to something like "Manual: {name} ({brand} {model})"
 *    so the user can find the research trail later.
 *  - Scans every assistant reply for URLs and emits them as
 *    {@link manualUrlSelected} events when the user clicks a chip.
 */
@Component({
  selector: 'app-manual-chat',
  imports: [FormsModule, MarkdownPipe],
  templateUrl: './manual-chat.component.html',
  styleUrl: './manual-chat.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManualChatComponent {

  private readonly chatService = inject(ChatService);

  readonly productContext = input<ProductContext | null>(null);
  readonly manualUrlSelected = output<string>();

  readonly messages = signal<ChatMessage[]>([]);
  readonly input = signal('');
  readonly streaming = signal(false);
  readonly chatId = signal<string | null>(null);

  readonly hasMessages = computed(() => this.messages().length > 0);

  /** Returns a list of unique URLs found in the given assistant message. */
  urlsFor(content: string): string[] {
    const matches = content.match(URL_REGEX) ?? [];
    // Strip trailing sentence/markdown punctuation that commonly abuts a URL.
    const cleaned = matches.map(u => u.replace(/[.,;:!?)>\]"']+$/, ''));
    return Array.from(new Set(cleaned));
  }

  selectUrl(url: string): void {
    this.manualUrlSelected.emit(url);
  }

  send(): void {
    const text = this.input().trim();
    if (!text || this.streaming()) return;

    const existingId = this.chatId();
    if (existingId) {
      this.dispatch(existingId, text);
      return;
    }

    // First message: create the chat, set a product-scoped title, then stream.
    this.streaming.set(true);
    this.chatService.createChat().subscribe({
      next: chat => {
        this.chatId.set(chat.chatId);
        const title = this.buildInitialTitle();
        if (title) {
          this.chatService.updateChatTitle(chat.chatId, title).subscribe({
            error: () => { /* title failure should not block the message */ },
          });
        }

        this.streaming.set(false);
        this.dispatch(chat.chatId, text);
      },
      error: () => this.streaming.set(false),
    });
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  private dispatch(chatId: string, text: string): void {
    this.messages.update(msgs => [
      ...msgs,
      { role: 'user', content: text },
      { role: 'assistant', content: '' },
    ]);
    this.input.set('');
    this.streaming.set(true);

    this.chatService.streamMessage(chatId, text).subscribe({
      next: delta => {
        this.messages.update(msgs => {
          const updated = [...msgs];
          const last = updated.at(-1)!;
          updated[updated.length - 1] = { ...last, content: last.content + delta };
          return updated;
        });
      },
      complete: () => this.streaming.set(false),
      error: () => this.streaming.set(false),
    });
  }

  private buildInitialTitle(): string {
    const ctx = this.productContext();
    const parts = ['Manual:', ctx?.name, ctx?.brand, ctx?.modelNumber].filter(Boolean) as string[];
    if (parts.length <= 1) {
      return '';
    }

    const title = parts.join(' ');
    return title.length > 60 ? `${title.slice(0, 60)}…` : title;
  }
}
