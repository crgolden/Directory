import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { vi } from 'vitest';
import { ManualChatPanelComponent } from './manual-chat-panel.component';

describe('ManualChatPanelComponent', () => {
  let fixture: ComponentFixture<ManualChatPanelComponent>;
  let component: ManualChatPanelComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ManualChatPanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(ManualChatPanelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders the collapsed Find Manual toggle by default', () => {
    expect(component.isOpen()).toBe(false);
    const toggle = fixture.debugElement.query(By.css('button.manual-chat-toggle'));
    expect(toggle).toBeTruthy();
    expect(toggle.nativeElement.textContent).toContain('Find Manual');

    // The expanded panel should not be in the DOM yet.
    expect(fixture.debugElement.query(By.css('.manual-chat-panel'))).toBeNull();
  });

  it('open() expands the panel and hides the toggle', () => {
    component.open();
    fixture.detectChanges();

    expect(component.isOpen()).toBe(true);
    expect(fixture.debugElement.query(By.css('.manual-chat-panel'))).toBeTruthy();
    expect(fixture.debugElement.query(By.css('button.manual-chat-toggle'))).toBeNull();
  });

  it('close() collapses the panel and shows the toggle again', () => {
    component.open();
    fixture.detectChanges();
    component.close();
    fixture.detectChanges();

    expect(component.isOpen()).toBe(false);
    expect(fixture.debugElement.query(By.css('button.manual-chat-toggle'))).toBeTruthy();
  });

  it('toggle() flips the open state', () => {
    component.toggle();
    expect(component.isOpen()).toBe(true);
    component.toggle();
    expect(component.isOpen()).toBe(false);
  });

  it('re-emits manualUrlSelected from the hosted chat component', () => {
    const emitted: string[] = [];
    component.manualUrlSelected.subscribe(v => emitted.push(v));
    component.onUrlSelected('https://example.com/manual.pdf');
    expect(emitted).toEqual(['https://example.com/manual.pdf']);
  });

  it('applies panel-narrow class when isNarrow() is true', () => {
    component.open();
    component.isNarrow.set(true);
    fixture.detectChanges();

    const panel = fixture.debugElement.query(By.css('.manual-chat-panel'));
    expect(panel.nativeElement.classList.contains('panel-narrow')).toBe(true);
  });

  it('omits panel-narrow class when isNarrow() is false', () => {
    component.open();
    component.isNarrow.set(false);
    fixture.detectChanges();

    const panel = fixture.debugElement.query(By.css('.manual-chat-panel'));
    expect(panel.nativeElement.classList.contains('panel-narrow')).toBe(false);
  });

  describe('responsive matchMedia behaviour', () => {
    afterEach(() => {
      vi.unstubAllGlobals();
    });

    it('sets isNarrow to true when matchMedia reports a narrow viewport at init', () => {
      const mockMql = {
        matches: true,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      } as unknown as MediaQueryList;
      vi.stubGlobal('matchMedia', vi.fn(() => mockMql));

      const narrowFixture = TestBed.createComponent(ManualChatPanelComponent);
      narrowFixture.detectChanges();

      expect(narrowFixture.componentInstance.isNarrow()).toBe(true);
      narrowFixture.destroy();
    });

    it('sets isNarrow to false when matchMedia reports a wide viewport at init', () => {
      const mockMql = {
        matches: false,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      } as unknown as MediaQueryList;
      vi.stubGlobal('matchMedia', vi.fn(() => mockMql));

      const wideFixture = TestBed.createComponent(ManualChatPanelComponent);
      wideFixture.detectChanges();

      expect(wideFixture.componentInstance.isNarrow()).toBe(false);
      wideFixture.destroy();
    });

    it('updates isNarrow when matchMedia fires a change event', () => {
      let capturedListener: ((e: MediaQueryListEvent) => void) | null = null;
      const mockMql = {
        matches: false,
        addEventListener: vi.fn((_type: string, fn: (e: MediaQueryListEvent) => void) => {
          capturedListener = fn;
        }),
        removeEventListener: vi.fn(),
      } as unknown as MediaQueryList;
      vi.stubGlobal('matchMedia', vi.fn(() => mockMql));

      const f = TestBed.createComponent(ManualChatPanelComponent);
      const c = f.componentInstance;
      f.detectChanges();

      expect(c.isNarrow()).toBe(false);

      capturedListener!({ matches: true } as MediaQueryListEvent);
      expect(c.isNarrow()).toBe(true);

      capturedListener!({ matches: false } as MediaQueryListEvent);
      expect(c.isNarrow()).toBe(false);

      f.destroy();
    });
  });
});
