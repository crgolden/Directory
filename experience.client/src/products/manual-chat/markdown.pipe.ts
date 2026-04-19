import { inject, Pipe, PipeTransform, SecurityContext } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { marked } from 'marked';

/**
 * Converts a Markdown string to sanitized HTML for display in the chat UI.
 *
 * Uses `marked` for Markdown-to-HTML conversion and Angular's `DomSanitizer`
 * to strip any unsafe tags or attributes before binding with `[innerHTML]`.
 * Safe for assistant message content produced by the OpenAI Responses API.
 */
@Pipe({
  name: 'markdown',
  standalone: true,
})
export class MarkdownPipe implements PipeTransform {
  private readonly sanitizer = inject(DomSanitizer);

  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }

    const html = marked.parse(value) as string;
    return this.sanitizer.sanitize(SecurityContext.HTML, html) ?? '';
  }
}
