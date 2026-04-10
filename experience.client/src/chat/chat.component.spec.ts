import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChatComponent } from './chat.component';
import { ChatService } from './chat.service';
import { By } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { of, Subject } from 'rxjs';
import { Chat } from './chat.model';

describe('ChatComponent', () => {
  let fixture: ComponentFixture<ChatComponent>;
  let mockService: Partial<ChatService>;
  let streamSubject: Subject<string>;

  const chat1: Chat = { chatId: 'chat-a', title: 'Chat A', createdAt: 1700000000 };
  const chat2: Chat = { chatId: 'chat-b', title: null, createdAt: 1699999000 };
  const newChat: Chat = { chatId: 'chat-new', title: null, createdAt: 1700001000 };

  const setup = async (queryParam: string | null = null, existingChats: Chat[] = []) => {
    streamSubject = new Subject<string>();
    mockService = {
      getChats: vi.fn(() => of(existingChats)),
      createChat: vi.fn(() => of(newChat)),
      getChat: vi.fn((chatId: string) => of({ chatId, title: null, createdAt: 0 })),
      streamMessage: vi.fn(() => streamSubject.asObservable()),
      deleteChat: vi.fn(() => of(void 0)),
      getChatMessages: vi.fn(() => of([])),
      updateChatTitle: vi.fn(() => of(void 0)),
      sendMessage: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [ChatComponent],
      providers: [
        { provide: ChatService, useValue: mockService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: { get: (key: string) => (key === 'q' ? queryParam : null) },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ChatComponent);
    fixture.detectChanges();
  };

  it('loads existing chats on init', async () => {
    await setup(null, [chat1, chat2]);
    expect(mockService.getChats).toHaveBeenCalledOnce();
    const buttons = fixture.debugElement.queryAll(By.css('button.chat-label'));
    expect(buttons).toHaveLength(2);
  });

  it('shows chat title in sidebar when available', async () => {
    await setup(null, [chat1]);
    const buttons = fixture.debugElement.queryAll(By.css('button.chat-label'));
    expect(buttons[0].nativeElement.textContent.trim()).toBe('Chat A');
  });

  it('falls back to truncated chatId when title is null', async () => {
    await setup(null, [chat2]);
    const buttons = fixture.debugElement.queryAll(By.css('button.chat-label'));
    expect(buttons[0].nativeElement.textContent.trim()).toBe('chat-b');
  });

  it('does not auto-create a chat on init', async () => {
    await setup();
    expect(mockService.createChat).not.toHaveBeenCalled();
    expect(fixture.componentInstance.chatId()).toBeNull();
  });

  it('pre-populates input from ?q= query param', async () => {
    await setup('Find the manual for my TV');
    expect(fixture.componentInstance.input()).toBe('Find the manual for my TV');
  });

  it('newChat creates a chat and adds it to the sidebar', async () => {
    await setup(null, [chat1]);
    fixture.componentInstance.newChat();
    fixture.detectChanges();

    expect(mockService.createChat).toHaveBeenCalledOnce();
    expect(fixture.componentInstance.chatId()).toBe('chat-new');
    const buttons = fixture.debugElement.queryAll(By.css('button.chat-label'));
    expect(buttons).toHaveLength(2); // chat-new prepended + chat1
    expect(buttons[0].nativeElement.textContent.trim()).toContain('chat-new');
  });

  it('selecting a different chat switches chatId and loads history', async () => {
    await setup(null, [chat1, chat2]);
    fixture.componentInstance.messages.set([{ role: 'user', content: 'Hi' }]);
    fixture.componentInstance.selectChat('chat-a');
    fixture.detectChanges();

    expect(fixture.componentInstance.chatId()).toBe('chat-a');
    expect(mockService.getChatMessages).toHaveBeenCalledWith('chat-a');
    expect(fixture.componentInstance.messages()).toEqual([]);
  });

  it('selectChat populates messages from history', async () => {
    await setup(null, [chat1]);
    (mockService.getChatMessages as ReturnType<typeof vi.fn>).mockReturnValueOnce(
      of([
        { role: 'user', text: 'Hello' },
        { role: 'assistant', text: 'Hi there!' },
        { role: null, text: null },        // filtered: null role
        { role: 'assistant', text: null }, // filtered: null text
      ])
    );
    fixture.componentInstance.selectChat('chat-a');
    fixture.detectChanges();

    const msgs = fixture.componentInstance.messages();
    expect(msgs).toHaveLength(2);
    expect(msgs[0]).toEqual({ role: 'user', content: 'Hello' });
    expect(msgs[1]).toEqual({ role: 'assistant', content: 'Hi there!' });
  });

  it('send appends user message and streams assistant response', async () => {
    await setup(null, [chat1]);
    fixture.componentInstance.chatId.set('chat-a');
    fixture.componentInstance.input.set('Hello');
    fixture.componentInstance.send();
    fixture.detectChanges();

    const messages = fixture.componentInstance.messages();
    expect(messages[0]).toEqual({ role: 'user', content: 'Hello' });
    expect(messages[1].role).toBe('assistant');

    streamSubject.next('Hi ');
    streamSubject.next('there!');
    streamSubject.complete();
    fixture.detectChanges();

    expect(fixture.componentInstance.messages()[1].content).toBe('Hi there!');
    expect(fixture.componentInstance.streaming()).toBe(false);
  });

  it('input is cleared after send', async () => {
    await setup(null, [chat1]);
    fixture.componentInstance.chatId.set('chat-a');
    fixture.componentInstance.input.set('Hello');
    fixture.componentInstance.send();
    expect(fixture.componentInstance.input()).toBe('');
  });

  it('Send button is disabled while streaming', async () => {
    await setup();
    fixture.componentInstance.input.set('Hello');
    fixture.componentInstance.streaming.set(true);
    fixture.detectChanges();

    const sendBtn = fixture.debugElement.query(By.css('button.btn-primary'));
    expect(sendBtn.nativeElement.disabled).toBe(true);
  });

  it('Send button is disabled when no chat is selected', async () => {
    await setup();
    fixture.componentInstance.input.set('Hello');
    fixture.detectChanges();

    const sendBtn = fixture.debugElement.query(By.css('button.btn-primary'));
    expect(sendBtn.nativeElement.disabled).toBe(true);
  });

  it('deleteChat removes chat from sidebar and clears selection when it was active', async () => {
    await setup(null, [chat1, chat2]);
    fixture.componentInstance.chatId.set('chat-a');

    fixture.componentInstance.deleteChat('chat-a');
    fixture.detectChanges();

    expect(mockService.deleteChat).toHaveBeenCalledWith('chat-a');
    expect(fixture.componentInstance.chatId()).toBeNull();
    expect(fixture.componentInstance.messages()).toEqual([]);
    const buttons = fixture.debugElement.queryAll(By.css('button.chat-label'));
    expect(buttons).toHaveLength(1); // only chat-b remains
  });

  it('deleteChat removes chat without clearing selection when a different chat is active', async () => {
    await setup(null, [chat1, chat2]);
    fixture.componentInstance.chatId.set('chat-a');

    fixture.componentInstance.deleteChat('chat-b');
    fixture.detectChanges();

    expect(fixture.componentInstance.chatId()).toBe('chat-a'); // unchanged
    const buttons = fixture.debugElement.queryAll(By.css('button.chat-label'));
    expect(buttons).toHaveLength(1); // only chat-a remains
  });

  it('startEditing shows an input with the current chat title', async () => {
    await setup(null, [chat1]);
    const fakeEvent = { stopPropagation: vi.fn() } as unknown as Event;

    fixture.componentInstance.startEditing(chat1, fakeEvent);
    fixture.detectChanges();

    expect(fixture.componentInstance.editingChatId()).toBe('chat-a');
    expect(fixture.componentInstance.editingTitle()).toBe('Chat A');
    const input = fixture.debugElement.query(By.css('.chat-title-input'));
    expect(input).not.toBeNull();
  });

  it('commitTitle saves new title and updates sidebar', async () => {
    await setup(null, [chat1]);
    fixture.componentInstance.editingChatId.set('chat-a');
    fixture.componentInstance.editingTitle.set('New Name');

    fixture.componentInstance.commitTitle('chat-a');
    fixture.detectChanges();

    expect(mockService.updateChatTitle).toHaveBeenCalledWith('chat-a', 'New Name');
    expect(fixture.componentInstance.editingChatId()).toBeNull();
    expect(fixture.componentInstance.chats().find(c => c.chatId === 'chat-a')?.title).toBe('New Name');
  });

  it('cancelEditing (Escape) exits edit mode without saving', async () => {
    await setup(null, [chat1]);
    fixture.componentInstance.editingChatId.set('chat-a');
    fixture.componentInstance.editingTitle.set('Changed');

    fixture.componentInstance.cancelEditing();
    fixture.detectChanges();

    expect(fixture.componentInstance.editingChatId()).toBeNull();
    expect(mockService.updateChatTitle).not.toHaveBeenCalled();
    // Original title is preserved in the chats signal
    expect(fixture.componentInstance.chats().find(c => c.chatId === 'chat-a')?.title).toBe('Chat A');
  });

  it('renders user message as plain text, not Markdown HTML', async () => {
    await setup(null, [chat1]);
    fixture.componentInstance.chatId.set('chat-a');
    fixture.componentInstance.messages.set([{ role: 'user', content: '**not bold**' }]);
    fixture.detectChanges();

    const bubble = fixture.debugElement.query(By.css('.user-bubble'));
    expect(bubble.nativeElement.textContent.trim()).toBe('**not bold**');
    expect(bubble.nativeElement.innerHTML).not.toContain('<strong>');
  });

  it('renders assistant message as Markdown HTML', async () => {
    await setup(null, [chat1]);
    fixture.componentInstance.chatId.set('chat-a');
    fixture.componentInstance.messages.set([{ role: 'assistant', content: '**bold text**' }]);
    fixture.detectChanges();

    const bubble = fixture.debugElement.query(By.css('.assistant-bubble'));
    expect(bubble.nativeElement.innerHTML).toContain('<strong>bold text</strong>');
  });

  it('shows typing cursor when assistant content is empty and streaming', async () => {
    await setup(null, [chat1]);
    fixture.componentInstance.chatId.set('chat-a');
    fixture.componentInstance.streaming.set(true);
    fixture.componentInstance.messages.set([{ role: 'assistant', content: '' }]);
    fixture.detectChanges();

    const bubble = fixture.debugElement.query(By.css('.assistant-bubble'));
    expect(bubble.nativeElement.querySelector('.typing-cursor')).not.toBeNull();
  });
});
