import { TestBed } from '@angular/core/testing';
import { MarkdownPipe } from './markdown.pipe';

describe('MarkdownPipe', () => {
  let pipe: MarkdownPipe;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    pipe = TestBed.runInInjectionContext(() => new MarkdownPipe());
  });

  it('returns empty string for null', () => {
    expect(pipe.transform(null)).toBe('');
  });

  it('returns empty string for undefined', () => {
    expect(pipe.transform(undefined)).toBe('');
  });

  it('returns empty string for empty string', () => {
    expect(pipe.transform('')).toBe('');
  });

  it('wraps plain text in a paragraph', () => {
    const result = pipe.transform('Hello world');
    expect(result).toContain('Hello world');
  });

  it('converts bold Markdown to strong element', () => {
    const result = pipe.transform('**bold**');
    expect(result).toContain('<strong>bold</strong>');
  });

  it('converts italic Markdown to em element', () => {
    const result = pipe.transform('*italic*');
    expect(result).toContain('<em>italic</em>');
  });

  it('converts heading Markdown to heading element', () => {
    const result = pipe.transform('### Section');
    expect(result).toContain('<h3>Section</h3>');
  });

  it('converts unordered list Markdown to ul/li elements', () => {
    const result = pipe.transform('- item one\n- item two');
    expect(result).toContain('<ul>');
    expect(result).toContain('<li>item one</li>');
    expect(result).toContain('<li>item two</li>');
  });

  it('converts ordered list Markdown to ol/li elements', () => {
    const result = pipe.transform('1. first\n2. second');
    expect(result).toContain('<ol>');
    expect(result).toContain('<li>first</li>');
  });

  it('converts inline code to code element', () => {
    const result = pipe.transform('use `dotnet test` to run');
    expect(result).toContain('<code>dotnet test</code>');
  });

  it('strips script tags (XSS protection)', () => {
    const result = pipe.transform('<script>alert("xss")</script>text');
    expect(result).not.toContain('<script>');
    expect(result).not.toContain('alert');
  });

  it('strips inline event handlers (XSS protection)', () => {
    const result = pipe.transform('<p onclick="alert(1)">click</p>');
    expect(result).not.toContain('onclick');
  });
});
