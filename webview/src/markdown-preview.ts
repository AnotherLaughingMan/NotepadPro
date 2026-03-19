import MarkdownIt from 'markdown-it';

const md = new MarkdownIt({
  html: false,        // Disallow raw HTML in untrusted content
  linkify: true,
  typographer: true,
  highlight(str, lang) {
    // Code blocks get a data-lang attribute; future enhancement could apply
    // Highlight.js or Shiki here. For now Monaco handles highlighting in the editor.
    const escaped = escapeHtml(str);
    const attr    = lang ? ` data-lang="${escapeHtml(lang)}"` : '';
    return `<pre${attr}><code>${escaped}</code></pre>`;
  },
});

export function renderMarkdown(content: string): string {
  return md.render(content);
}

export function updatePreviewPane(pane: HTMLElement, content: string): void {
  pane.innerHTML = renderMarkdown(content);
  // Scroll back to top on full content refresh
  pane.scrollTop = 0;
}

function escapeHtml(str: string): string {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
