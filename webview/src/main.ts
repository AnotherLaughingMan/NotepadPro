// Configure Monaco workers BEFORE any monaco-editor import resolves them.
// The ?worker Vite transform produces a Worker constructor from the module URL.
import EditorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker';
import JsonWorker   from 'monaco-editor/esm/vs/language/json/json.worker?worker';
import CssWorker    from 'monaco-editor/esm/vs/language/css/css.worker?worker';
import HtmlWorker   from 'monaco-editor/esm/vs/language/html/html.worker?worker';
import TsWorker     from 'monaco-editor/esm/vs/language/typescript/ts.worker?worker';

import 'monaco-editor/esm/vs/basic-languages/monaco.contribution.js';
import 'monaco-editor/esm/vs/language/css/monaco.contribution.js';
import 'monaco-editor/esm/vs/language/html/monaco.contribution.js';
import 'monaco-editor/esm/vs/language/json/monaco.contribution.js';
import 'monaco-editor/esm/vs/language/typescript/monaco.contribution.js';

(self as unknown as Record<string, unknown>)['MonacoEnvironment'] = {
  getWorker(_: unknown, label: string): Worker {
    if (label === 'json')                               return new JsonWorker();
    if (label === 'css' || label === 'scss' || label === 'less') return new CssWorker();
    if (label === 'html' || label === 'handlebars' || label === 'razor') return new HtmlWorker();
    if (label === 'typescript' || label === 'javascript') return new TsWorker();
    return new EditorWorker();
  },
};

import * as monaco from 'monaco-editor';
import { bridge }               from './bridge';
import { createEditor, applyBookmarks, applySettings, setContent, getCurrentContent, getCurrentLanguage } from './editor';
import { applyTheme }           from './theme';
import { updatePreviewPane }    from './markdown-preview';
import { mountWelcome }         from './welcome';
import type { EditorSettings }  from './types';

const requiredLanguageIds = [
  'plaintext',
  'c',
  'cpp',
  'csharp',
  'css',
  'go',
  'html',
  'java',
  'javascript',
  'json',
  'kotlin',
  'markdown',
  'php',
  'powershell',
  'python',
  'r',
  'ruby',
  'rust',
  'shell',
  'sql',
  'swift',
  'typescript',
  'xml',
  'yaml',
] as const;

// ── DOM refs ──────────────────────────────────────────────────────────────
const editorEl    = document.getElementById('monaco-editor')   as HTMLElement;
const containerEl = document.getElementById('editor-container') as HTMLElement;
const previewEl   = document.getElementById('preview-pane')    as HTMLElement;
const welcomeEl   = document.getElementById('welcome-view')    as HTMLElement;

// ── Defaults (overridden immediately by host via 'settings:apply') ────────
const defaultSettings: EditorSettings = {
  wordWrap:         false,
  showLineNumbers:  true,
  isMinimapVisible: true,
  minimapFadeSpeedMs: 140,
  autoIndentation:  true,
  autoBracketing:   true,
  renderWhitespace: false,
  editorFontSize:   13,
  indentation:      '    ',
  eol:              'LF',
};

const editor = createEditor(editorEl, defaultSettings);

const registeredLanguageIds = monaco.languages.getLanguages()
  .map(language => language.id)
  .sort((left, right) => left.localeCompare(right));

const missingLanguageIds = requiredLanguageIds.filter(languageId => !registeredLanguageIds.includes(languageId));

(window as unknown as Record<string, unknown>)['__LAST_EDITOR'] = editor;
(window as unknown as Record<string, unknown>)['__notepadproDiagnostics'] = {
  requiredLanguageIds,
  registeredLanguageIds,
  missingLanguageIds,
  syntaxHighlightingReady: missingLanguageIds.length === 0,
};

if (missingLanguageIds.length > 0) {
  console.error('[NotepadPro] Monaco language registration incomplete.', {
    missingLanguageIds,
    registeredLanguageIds,
  });
}

// ── State ─────────────────────────────────────────────────────────────────
let savedContent     = '';
let isPreviewVisible = false;
let isMinimapEnabled = defaultSettings.isMinimapVisible;
let isMinimapPointerOver = false;
let minimapScrollRevealTimer = 0;
let minimapFadeSpeedMs = Math.max(60, defaultSettings.minimapFadeSpeedMs);

const MINIMAP_IDLE_OPACITY = 0;
const MINIMAP_ACTIVE_OPACITY = 1;

function setMinimapOpacity(opacity: number): void {
  const clamped = Math.max(0, Math.min(1, opacity));
  document.documentElement.style.setProperty('--monaco-minimap-opacity', clamped.toString());
}

function updateMinimapVisibilityState(forceActive: boolean = false): void {
  if (!isMinimapEnabled) {
    setMinimapOpacity(0);
    return;
  }

  setMinimapOpacity(forceActive || isMinimapPointerOver ? MINIMAP_ACTIVE_OPACITY : MINIMAP_IDLE_OPACITY);
}

function setMinimapFadeSpeed(ms: number): void {
  minimapFadeSpeedMs = Math.max(60, Math.min(2000, ms));
  document.documentElement.style.setProperty('--monaco-minimap-fade-duration-ms', `${minimapFadeSpeedMs}ms`);
}

function revealMinimapFromScroll(): void {
  if (!isMinimapEnabled) {
    return;
  }

  clearTimeout(minimapScrollRevealTimer);
  updateMinimapVisibilityState(true);
  minimapScrollRevealTimer = window.setTimeout(() => {
    minimapScrollRevealTimer = 0;
    updateMinimapVisibilityState(false);
  }, Math.max(80, Math.round(minimapFadeSpeedMs * 1.8)));
}

function updateMinimapPointerState(clientX: number): void {
  if (!isMinimapEnabled) {
    isMinimapPointerOver = false;
    return;
  }

  const rect = editorEl.getBoundingClientRect();
  if (rect.width <= 0) {
    isMinimapPointerOver = false;
    updateMinimapVisibilityState(false);
    return;
  }

  const layout = editor.getLayoutInfo();
  const minimapLeft = layout.minimap.minimapLeft;
  const minimapRight = minimapLeft + layout.minimap.minimapWidth;
  const relativeX = clientX - rect.left;

  const isOver = relativeX >= minimapLeft && relativeX <= minimapRight;
  if (isOver === isMinimapPointerOver) {
    return;
  }

  isMinimapPointerOver = isOver;
  updateMinimapVisibilityState(false);
}

editor.onDidScrollChange(() => {
  revealMinimapFromScroll();
});

editorEl.addEventListener('pointermove', (ev: PointerEvent) => {
  updateMinimapPointerState(ev.clientX);
});

editorEl.addEventListener('pointerleave', () => {
  if (!isMinimapPointerOver) {
    return;
  }

  isMinimapPointerOver = false;
  updateMinimapVisibilityState(false);
});

// ── Editor event → host ───────────────────────────────────────────────────

editor.onDidChangeModelContent(() => {
  const content  = getCurrentContent();
  const isDirty  = content !== savedContent;
  bridge.post({ type: 'file:modified', isDirty });

  if (isPreviewVisible) {
    updatePreviewPane(previewEl, content);
  }
});

editor.onDidChangeCursorPosition(e => {
  const selection  = editor.getSelection();
  const model      = editor.getModel();
  const selLength  = selection && model
    ? model.getValueLengthInRange(selection)
    : 0;

  bridge.post({
    type:            'cursor:changed',
    line:             e.position.lineNumber,
    column:           e.position.column,
    selectionLength:  selLength,
  });
});

// Debounce word-count updates to avoid hammering the host on every keystroke
let wordCountTimer = 0;
editor.onDidChangeModelContent(() => {
  clearTimeout(wordCountTimer);
  wordCountTimer = window.setTimeout(() => {
    const content = getCurrentContent();
    const words   = content.trim() === '' ? 0 : content.trim().split(/\s+/).length;
    const lines   = editor.getModel()?.getLineCount() ?? 0;
    bridge.post({ type: 'status:update', wordCount: words, language: getCurrentLanguage(), lineCount: lines });
  }, 300);
});

// Ctrl+S → ask host to save
editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, () => {
  bridge.post({ type: 'file:save:request', content: getCurrentContent() });
});

// ── Host → editor messages ────────────────────────────────────────────────

bridge.on(msg => {
  switch (msg.type) {
    case 'editor:scrollbarOpacity': {
      // Clamp and set CSS variable for Monaco scrollbars
      const opacity = Math.max(0.1, Math.min(1, msg.opacity));
      document.documentElement.style.setProperty('--monaco-scrollbar-opacity', opacity.toString());
      break;
    }
    case 'file:open':
      // Set savedContent before setContent to avoid transient dirty events
      // fired by Monaco while swapping models/content.
      savedContent = msg.content;
      setContent(msg.content, msg.language);
      if (isPreviewVisible) updatePreviewPane(previewEl, msg.content);
      break;

    case 'file:saved':
      savedContent = getCurrentContent();
      break;

    case 'settings:apply':
      applySettings(msg.settings);
      isMinimapEnabled = msg.settings.isMinimapVisible;
      setMinimapFadeSpeed(msg.settings.minimapFadeSpeedMs);
      if (!isMinimapEnabled) {
        isMinimapPointerOver = false;
        clearTimeout(minimapScrollRevealTimer);
        minimapScrollRevealTimer = 0;
      }
      updateMinimapVisibilityState(false);
      break;

    case 'theme:apply':
      applyTheme(msg.theme, msg.colors);
      break;

    case 'editor:navigate': {
      const line = msg.line;
      const col  = msg.column ?? 1;
      editor.revealLineInCenter(line);
      editor.setPosition({ lineNumber: line, column: col });
      editor.focus();
      break;
    }

    case 'editor:bookmarks':
      applyBookmarks(msg.bookmarks);
      break;

    case 'editor:command':
      editor.getAction(msg.command as string)?.run();
      break;

    case 'preview:toggle':
      isPreviewVisible = msg.visible;
      containerEl.classList.toggle('preview-visible', msg.visible);
      if (msg.visible) {
        updatePreviewPane(previewEl, getCurrentContent());
      }
      break;

    case 'view:show':
      if (msg.view === 'welcome') {
        containerEl.style.display = 'none';
        welcomeEl.style.display   = 'flex';
        mountWelcome(welcomeEl, msg.data);
      } else {
        welcomeEl.style.display   = 'none';
        containerEl.style.display = 'flex';
        editor.focus();
      }
      break;
  }
});

// ── Ready signal ──────────────────────────────────────────────────────────
bridge.post({ type: 'editor:ready' });
