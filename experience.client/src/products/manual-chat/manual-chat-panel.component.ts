import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { ManualChatComponent } from './manual-chat.component';
import { ProductContext } from './chat.model';

/**
 * Retractable side panel that hosts {@link ManualChatComponent}.
 *
 * Layout:
 *  - Collapsed: a fixed-position "Find Manual" tab anchored to the right edge
 *    of the viewport.
 *  - Expanded on wide screens (≥ 768px): a sidebar that slides in next to the
 *    product form.
 *  - Expanded on narrow screens (< 768px): a full-screen overlay so the form
 *    is never cramped.
 *
 * The panel is only instantiated while the user is on `/products/new` or
 * `/products/:id/edit`. When collapsed, no `ChatService` calls are issued.
 */
@Component({
  selector: 'app-manual-chat-panel',
  imports: [ManualChatComponent],
  templateUrl: './manual-chat-panel.component.html',
  styleUrl: './manual-chat-panel.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManualChatPanelComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);

  readonly productContext = input<ProductContext | null>(null);
  readonly manualUrlSelected = output<string>();

  readonly isOpen = signal(false);
  readonly isNarrow = signal(false);

  ngOnInit(): void {
    // Guard against non-browser environments (SSR, jsdom without matchMedia).
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return;
    }

    const media = window.matchMedia('(max-width: 767px)');
    this.isNarrow.set(media.matches);

    const listener = (event: MediaQueryListEvent) => this.isNarrow.set(event.matches);
    media.addEventListener('change', listener);
    this.destroyRef.onDestroy(() => media.removeEventListener('change', listener));
  }

  open(): void {
    this.isOpen.set(true);
  }

  close(): void {
    this.isOpen.set(false);
  }

  toggle(): void {
    this.isOpen.update(v => !v);
  }

  onUrlSelected(url: string): void {
    this.manualUrlSelected.emit(url);
  }
}
