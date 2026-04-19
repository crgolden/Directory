import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { ManualChatComponent } from './manual-chat.component';
import { ChatService } from './chat.service';
import { of } from 'rxjs';

describe('ManualChatComponent', () => {
  let fixture: ComponentFixture<ManualChatComponent>;
  let component: ManualChatComponent;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ManualChatComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(ManualChatComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('renders the empty-state prompt when there are no messages', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Ask me to find a user manual');
  });

  it('send button is disabled when input is empty', () => {
    const btn = fixture.debugElement.query(By.css('button.btn-primary'));
    expect(btn.nativeElement.disabled).toBe(true);
  });

  it('urlsFor extracts unique http(s) URLs from assistant content', () => {
    const urls = component.urlsFor(
      'Try https://example.com/a.pdf or https://example.com/b.pdf. Duplicate: https://example.com/a.pdf'
    );
    expect(urls).toEqual([
      'https://example.com/a.pdf',
      'https://example.com/b.pdf',
    ]);
  });

  it('urlsFor strips trailing markdown/paren punctuation', () => {
    const urls = component.urlsFor('See [link](https://example.com/manual.pdf).');
    expect(urls).toEqual(['https://example.com/manual.pdf']);
  });

  it('selectUrl emits manualUrlSelected', () => {
    const emitted: string[] = [];
    component.manualUrlSelected.subscribe(v => emitted.push(v));
    component.selectUrl('https://example.com/picked.pdf');
    expect(emitted).toEqual(['https://example.com/picked.pdf']);
  });

  it('first send() creates a chat then streams to the new chatId', () => {
    const chatService = TestBed.inject(ChatService);
    const streamSpy = vi.spyOn(chatService, 'streamMessage').mockReturnValue(of());

    // Act: type a message and send. No productContext is provided, so no PATCH-title call fires.
    component.input.set('Find me the manual');
    component.send();

    // Assert: createChat fired first.
    const createReq = httpMock.expectOne('/manuals/api/chats');
    expect(createReq.request.method).toBe('POST');
    createReq.flush({ chatId: 'chat-xyz', title: null, createdAt: 1 });

    // And stream was kicked off for the newly created chat.
    expect(streamSpy).toHaveBeenCalledWith('chat-xyz', 'Find me the manual');
    expect(component.chatId()).toBe('chat-xyz');
  });

  it('subsequent send() reuses the existing chat id', () => {
    const chatService = TestBed.inject(ChatService);
    const createSpy = vi.spyOn(chatService, 'createChat');
    const streamSpy = vi.spyOn(chatService, 'streamMessage').mockReturnValue(of());

    component.chatId.set('already-exists');
    component.input.set('Second question');
    component.send();

    expect(createSpy).not.toHaveBeenCalled();
    expect(streamSpy).toHaveBeenCalledWith('already-exists', 'Second question');
  });

  it('streamed deltas append to the last assistant message', () => {
    const chatService = TestBed.inject(ChatService);
    vi.spyOn(chatService, 'streamMessage').mockReturnValue(of('Hello', ' world'));

    component.chatId.set('chat-1');
    component.input.set('Hi');
    component.send();

    const last = component.messages().at(-1)!;
    expect(last.role).toBe('assistant');
    expect(last.content).toBe('Hello world');
  });
});
