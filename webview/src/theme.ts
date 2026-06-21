import * as monaco from 'monaco-editor';
import type { ThemeColors } from './types';

export function applyTheme(name: string, colors: ThemeColors): void {
  const base: monaco.editor.BuiltinTheme =
    name.toLowerCase().includes('light') ? 'vs' : 'vs-dark';
  const colorScheme = base === 'vs' ? 'light' : 'dark';

  monaco.editor.defineTheme('notepadpro-theme', {
    base,
    inherit: true,
    rules: [
      { token: 'keyword',               foreground: hex(colors.syntaxKeyword) },
      { token: 'keyword.operator',      foreground: hex(colors.syntaxKeyword) },
      { token: 'storage.type',          foreground: hex(colors.syntaxKeyword), fontStyle: '' },
      { token: 'string',                foreground: hex(colors.syntaxString) },
      { token: 'string.quoted',         foreground: hex(colors.syntaxString) },
      { token: 'comment',               foreground: hex(colors.syntaxComment), fontStyle: 'italic' },
      { token: 'comment.line',          foreground: hex(colors.syntaxComment), fontStyle: 'italic' },
      { token: 'comment.block',         foreground: hex(colors.syntaxComment), fontStyle: 'italic' },
      { token: 'number',                foreground: hex(colors.syntaxNumber) },
      { token: 'constant.numeric',      foreground: hex(colors.syntaxNumber) },
      { token: 'entity.name.type',      foreground: hex(colors.syntaxType), fontStyle: 'bold' },
      { token: 'support.type',          foreground: hex(colors.syntaxType) },
      { token: 'entity.name.function',  foreground: hex(colors.syntaxFunction) },
      { token: 'support.function',      foreground: hex(colors.syntaxFunction) },
      { token: 'meta.function-call',    foreground: hex(colors.syntaxFunction) },
    ],
    colors: {
      'editor.background':                   colors.background,
      'editor.foreground':                   colors.foreground,
      'editor.selectionBackground':          colors.selectionBackground,
      'editor.lineHighlightBackground':      colors.lineHighlight,
      'editorLineNumber.foreground':         colors.foreground + '66',
      'editorLineNumber.activeForeground':   colors.foreground,
      'editor.selectionHighlightBackground': colors.selectionBackground + '80',
      'editorCursor.foreground':             colors.foreground,
      'editorWhitespace.foreground':         colors.foreground + '28',
      'editorIndentGuide.background1':       colors.foreground + '20',
      'editorIndentGuide.activeBackground1': colors.foreground + '50',
      'editorBracketMatch.background':       colors.selectionBackground + '60',
      'editorBracketMatch.border':           colors.syntaxKeyword + '80',
      'editor.scrollbarSlider.background':   colors.foreground + '33',
      'editor.scrollbarSlider.hoverBackground': colors.foreground + '55',
      'editor.scrollbarSlider.activeBackground': colors.foreground + '77',
      'scrollbar.shadow':                    colors.background,
    },
  });

  monaco.editor.setTheme('notepadpro-theme');

  // Sync page background to avoid flash of wrong color
  document.documentElement.style.setProperty('--editor-bg',    colors.background);
  document.documentElement.style.setProperty('--editor-fg',    colors.foreground);
  document.documentElement.style.setProperty('--border-color', colors.foreground + '30');
  document.documentElement.style.setProperty('--page-color-scheme', colorScheme);
  document.documentElement.style.setProperty('--preview-color-scheme', colorScheme);
  document.documentElement.style.setProperty('--preview-scrollbar-track', colors.background + '2E');
  document.documentElement.style.setProperty('--preview-scrollbar-thumb', colors.foreground + '66');
  document.documentElement.style.setProperty('--preview-scrollbar-thumb-hover', colors.foreground + '99');
  document.body.style.background = colors.background;
  document.documentElement.style.colorScheme = colorScheme;
}

/** Strips leading '#' from a hex color string. Monaco token rules require bare hex. */
function hex(color: string): string {
  return color.startsWith('#') ? color.slice(1) : color;
}
