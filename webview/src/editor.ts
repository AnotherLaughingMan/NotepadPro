import * as monaco from 'monaco-editor';
import type { BookmarkMarker, EditorSettings } from './types';

let _editor: monaco.editor.IStandaloneCodeEditor | null = null;
let _currentLanguage = 'plaintext';
let _bookmarkDecorationIds: string[] = [];

export function createEditor(
  container: HTMLElement,
  settings: EditorSettings,
): monaco.editor.IStandaloneCodeEditor {
  _editor = monaco.editor.create(container, {
    value: '',
    language: 'plaintext',
    theme: 'notepadpro-theme',
    ...mapSettings(settings),
    automaticLayout: true,
    scrollBeyondLastLine: false,
    renderWhitespace: 'selection',
    bracketPairColorization: { enabled: true },
    guides: { bracketPairs: true, indentation: true },
    folding: true,
    foldingHighlight: true,
    suggest: { showWords: true },
    padding: { top: 8, bottom: 8 },
    smoothScrolling: true,
    cursorSmoothCaretAnimation: 'on',
    stickyScroll: { enabled: true },
    scrollbar: {
      verticalScrollbarSize: 10,
      horizontalScrollbarSize: 10,
      useShadows: false,
    },
  });

  return _editor;
}

export function getEditor(): monaco.editor.IStandaloneCodeEditor {
  if (!_editor) throw new Error('Editor not yet initialized');
  return _editor;
}

export function setContent(content: string, language: string): void {
  if (!_editor) return;
  _currentLanguage = language;
  const model = _editor.getModel();
  if (model) {
    monaco.editor.setModelLanguage(model, toMonacoLanguage(language));
  }
  // setValue resets undo history — correct for a fresh file open
  _editor.setValue(content);
  _editor.setScrollTop(0);
}

export function applyBookmarks(bookmarks: BookmarkMarker[]): void {
  if (!_editor) return;

  const model = _editor.getModel();
  if (!model) {
    _bookmarkDecorationIds = [];
    return;
  }

  const decorations: monaco.editor.IModelDeltaDecoration[] = bookmarks.map(bookmark => {
    const lineNumber = Math.max(1, Math.min(bookmark.line, model.getLineCount()));
    const className = bookmark.state === 'global'
      ? 'bookmark-glyph-global'
      : bookmark.state === 'stale'
        ? 'bookmark-glyph-stale'
        : 'bookmark-glyph-scoped';

    return {
      range: new monaco.Range(lineNumber, 1, lineNumber, 1),
      options: {
        isWholeLine: false,
        glyphMarginClassName: className,
        glyphMarginHoverMessage: { value: bookmark.state === 'global' ? 'Global bookmark' : bookmark.state === 'stale' ? 'Stale bookmark' : 'Bookmark' },
      },
    };
  });

  _bookmarkDecorationIds = _editor.deltaDecorations(_bookmarkDecorationIds, decorations);
  _editor.updateOptions({ glyphMargin: bookmarks.length > 0 });
}

/** Maps C# display language names to Monaco language IDs. */
function toMonacoLanguage(displayName: string): string {
  const key = displayName.toLowerCase().trim();
  const map: Record<string, string> = {
    'c#':          'csharp',
    'c++':         'cpp',
    'c':           'c',
    'javascript':  'javascript',
    'typescript':  'typescript',
    'json':        'json',
    'markdown':    'markdown',
    'html':        'html',
    'css':         'css',
    'scss':        'scss',
    'less':        'less',
    'xml':         'xml',
    'xaml':        'xml',
    'axaml':       'xml',
    'yaml':        'yaml',
    'python':      'python',
    'lua':         'lua',
    'rust':        'rust',
    'go':          'go',
    'java':        'java',
    'kotlin':      'kotlin',
    'swift':       'swift',
    'php':         'php',
    'ruby':        'ruby',
    'shell':       'shell',
    'powershell':  'powershell',
    'sql':         'sql',
    'r':           'r',
    'plain text':  'plaintext',
  };
  return map[key] ?? key.replace(/\s+/g, '');
}

export function getCurrentContent(): string {
  return _editor?.getValue() ?? '';
}

export function getCurrentLanguage(): string {
  return _currentLanguage;
}

export function applySettings(settings: EditorSettings): void {
  if (!_editor) return;
  _editor.updateOptions(mapSettings(settings));
  // tabSize and insertSpaces are text-model options, not editor options
  const useSpaces = !settings.indentation.startsWith('\t');
  const tabSize   = useSpaces ? (settings.indentation.length || 4) : 4;
  _editor.getModel()?.updateOptions({ tabSize, insertSpaces: useSpaces });
}

function mapSettings(s: EditorSettings): monaco.editor.IEditorOptions {
  return {
    wordWrap:             s.wordWrap ? 'on' : 'off',
    lineNumbers:          s.showLineNumbers ? 'on' : 'off',
    glyphMargin:          true,
    minimap:              { enabled: s.isMinimapVisible },
    autoIndent:           s.autoIndentation ? 'full' : 'brackets',
    autoClosingBrackets:  s.autoBracketing  ? 'always' : 'never',
    autoClosingQuotes:    s.autoBracketing  ? 'always' : 'never',
    renderWhitespace:     s.renderWhitespace ? 'all' : 'selection',
    fontSize:             s.editorFontSize,
  };
}
