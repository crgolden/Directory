import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChatComponent } from './chat.component';
import { ChatService } from './chat.service';
import { By } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { of, Subject } from 'rxjs';

describe('ChatComponent', () => {
  let fixture: ComponentFixture<ChatComponent>;
  let mockService: Partial<ChatService>;
  let streamSubject: Subject<string>;

  const setup = async (queryParam: string | null = null, existingConversations: string[] = []) => {
    streamSubject = new Subject<string>();
    mockService = {
      getConversations: vi.fn(() => of(existingConversations)),
      createConversation: vi.fn(() => of('conv-test')),
      sendMessage: vi.fn(),
      streamMessage: vi.fn(() => streamSubject.asObservable()),
      deleteConversation: vi.fn(() => of(void 0)),
      getConversationItems: vi.fn(() => of([])),
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

  it('loads existing conversations on init', async () => {
    await setup(null, ['conv-a', 'conv-b']);
    expect(mockService.getConversations).toHaveBeenCalledOnce();
    const buttons = fixture.debugElement.queryAll(By.css('button.conversation-item'));
    // 'conv-test' is prepended by startConversation; existing ones follow
    expect(buttons.length).toBeGreaterThanOrEqual(2);
  });

  it('creates a conversation on init', async () => {
    await setup();
    expect(mockService.createConversation).toHaveBeenCalledOnce();
    expect(fixture.componentInstance.conversationId()).toBe('conv-test');
  });

  it('pre-populates input from ?q= query param', async () => {
    await setup('Find the manual for my TV');
    expect(fixture.componentInstance.input()).toBe('Find the manual for my TV');
  });

  it('selecting a different conversation switches conversationId and loads history', async () => {
    await setup(null, ['conv-old']);
    fixture.componentInstance.messages.set([{ role: 'user', content: 'Hi' }]);
    fixture.componentInstance.selectConversation('conv-old');
    fixture.detectChanges();

    expect(fixture.componentInstance.conversationId()).toBe('conv-old');
    expect(mockService.getConversationItems).toHaveBeenCalledWith('conv-old');
    // mock returns [] so messages end up empty
    expect(fixture.componentInstance.messages()).toEqual([]);
  });

  it('selectConversation populates messages from history', async () => {
    await setup(null, ['conv-history']);
    (mockService.getConversationItems as ReturnType<typeof vi.fn>).mockReturnValueOnce(
      of([
        { id: 'i1', role: 'user', text: 'Hello' },
        { id: 'i2', role: 'assistant', text: 'Hi there!' },
        { id: 'i3', role: null, text: null },       // filtered: null role
        { id: 'i4', role: 'assistant', text: null }, // filtered: null text
      ])
    );
    fixture.componentInstance.selectConversation('conv-history');
    fixture.detectChanges();

    const msgs = fixture.componentInstance.messages();
    expect(msgs).toHaveLength(2);
    expect(msgs[0]).toEqual({ role: 'user', content: 'Hello' });
    expect(msgs[1]).toEqual({ role: 'assistant', content: 'Hi there!' });
  });

  it('send appends user message and streams assistant response', async () => {
    await setup();
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
    await setup();
    fixture.componentInstance.input.set('Hello');
    fixture.componentInstance.send();
    expect(fixture.componentInstance.input()).toBe('');
  });

  it('newConversation clears messages and creates a new conversationId', async () => {
    await setup();
    fixture.componentInstance.messages.set([{ role: 'user', content: 'Hi' }]);
    fixture.componentInstance.newConversation();
    fixture.detectChanges();

    expect(fixture.componentInstance.messages()).toEqual([]);
    expect(mockService.createConversation).toHaveBeenCalledTimes(2);
  });

  it('Send button is disabled while streaming', async () => {
    await setup();
    fixture.componentInstance.input.set('Hello');
    fixture.componentInstance.streaming.set(true);
    fixture.detectChanges();

    const sendBtn = fixture.debugElement.query(By.css('button.btn-primary'));
    expect(sendBtn.nativeElement.disabled).toBe(true);
  });
});
