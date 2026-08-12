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
import {
  mountEditableMarkdown,
  setEditableMarkdownEnabled,
  setEditableMarkdownContent,
  applyEditableMarkdownCommand,
  getEditableMarkdownSelection,
  setEditableMarkdownSelection,
  type EditableMarkdownSelection,
} from './markdown-editable';
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
const previewScrollbarEl = document.getElementById('preview-scrollbar') as HTMLElement;
const previewScrollbarThumbEl = document.getElementById('preview-scrollbar-thumb') as HTMLElement;
const splitterEl  = document.getElementById('preview-splitter') as HTMLElement;
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
let isMarkdownDocument = false;
let isMinimapEnabled = defaultSettings.isMinimapVisible;
let isMinimapPointerOver = false;
let minimapScrollRevealTimer = 0;
let minimapFadeSpeedMs = Math.max(60, defaultSettings.minimapFadeSpeedMs);
let previewScrollbarDragging = false;
let previewScrollbarDragStartY = 0;
let previewScrollbarDragStartScrollTop = 0;
let previewScrollbarHideTimer = 0;

const MINIMAP_IDLE_OPACITY = 0;
const MINIMAP_ACTIVE_OPACITY = 1;
const MAX_EDITABLE_MARKDOWN_CHARS = 300000;
const PREVIEW_SCROLLBAR_MIN_THUMB_HEIGHT = 24;

let sourceSelectionBeforeRenderedToggle: monaco.Selection | null = null;

mountEditableMarkdown(previewEl, markdown => {
  applyRenderedMarkdownTextToSource(markdown);
  bridge.post({ type: 'markdown:content:update', content: markdown, sourceMode: 'rendered' });
  updatePreviewScrollbar();
});

function updatePreviewScrollbar(): void {
  if (!isPreviewVisible || !previewScrollbarEl || !previewScrollbarThumbEl) {
    return;
  }

  const scrollHeight = previewEl.scrollHeight;
  const clientHeight = previewEl.clientHeight;
  if (scrollHeight <= clientHeight + 1) {
    previewScrollbarEl.style.display = 'none';
    return;
  }

  const trackHeight = previewScrollbarEl.getBoundingClientRect().height || previewEl.clientHeight;
  const thumbHeight = Math.max(
    PREVIEW_SCROLLBAR_MIN_THUMB_HEIGHT,
    Math.round(trackHeight * clientHeight / Math.max(scrollHeight, 1)),
  );
  const maxThumbTop = Math.max(1, trackHeight - thumbHeight);
  const maxScrollTop = Math.max(1, scrollHeight - clientHeight);
  const thumbTop = Math.round((previewEl.scrollTop / maxScrollTop) * maxThumbTop);

  previewScrollbarEl.style.display = 'block';
  previewScrollbarEl.dataset.active = 'true';
  previewScrollbarThumbEl.style.height = `${thumbHeight}px`;
  previewScrollbarThumbEl.style.transform = `translateY(${thumbTop}px)`;
}

function schedulePreviewScrollbarFade(): void {
  if (!previewScrollbarEl) {
    return;
  }

  clearTimeout(previewScrollbarHideTimer);
  previewScrollbarEl.dataset.active = 'true';
  previewScrollbarHideTimer = window.setTimeout(() => {
    if (!isPreviewVisible || previewScrollbarDragging) {
      return;
    }

    previewScrollbarEl.dataset.active = 'false';
  }, 850);
}

function updatePreviewScrollbarVisibility(): void {
  if (!previewScrollbarEl || !isPreviewVisible) {
    return;
  }

  updatePreviewScrollbar();
}

function scrollPreviewToScrollbarPosition(clientY: number): void {
  if (!previewScrollbarEl || !previewScrollbarThumbEl) {
    return;
  }

  const trackRect = previewScrollbarEl.getBoundingClientRect();
  const thumbRect = previewScrollbarThumbEl.getBoundingClientRect();
  const maxThumbTop = Math.max(1, trackRect.height - thumbRect.height);
  const maxScrollTop = Math.max(1, previewEl.scrollHeight - previewEl.clientHeight);
  const relativeY = Math.max(0, Math.min(trackRect.height, clientY - trackRect.top));
  const nextScrollTop = Math.round((relativeY / maxThumbTop) * maxScrollTop);
  previewEl.scrollTop = nextScrollTop;
  updatePreviewScrollbar();
}

previewEl.addEventListener('scroll', () => {
  updatePreviewScrollbar();
  schedulePreviewScrollbarFade();
  revealMinimapFromScroll();
});

previewEl.addEventListener('input', () => {
  updatePreviewScrollbar();
});

previewEl.addEventListener('mousemove', () => {
  if (isPreviewVisible) {
    schedulePreviewScrollbarFade();
  }
});

window.addEventListener('resize', () => {
  updatePreviewScrollbar();
});

if (previewScrollbarEl && previewScrollbarThumbEl) {
  previewScrollbarThumbEl.addEventListener('pointerdown', event => {
    if (!isPreviewVisible) {
      return;
    }

    previewScrollbarDragging = true;
    previewScrollbarDragStartY = event.clientY;
    previewScrollbarDragStartScrollTop = previewEl.scrollTop;
    previewScrollbarEl.dataset.dragging = 'true';
    previewScrollbarEl.dataset.active = 'true';
    previewScrollbarThumbEl.setPointerCapture(event.pointerId);
    event.preventDefault();
  });

  previewScrollbarEl.addEventListener('pointerenter', () => {
    if (isPreviewVisible) {
      clearTimeout(previewScrollbarHideTimer);
      previewScrollbarEl.dataset.active = 'true';
    }
  });

  previewScrollbarEl.addEventListener('pointerleave', () => {
    if (isPreviewVisible && !previewScrollbarDragging) {
      schedulePreviewScrollbarFade();
    }
  });

  previewScrollbarEl.addEventListener('pointermove', event => {
    if (!previewScrollbarDragging) {
      return;
    }

    const trackRect = previewScrollbarEl.getBoundingClientRect();
    const thumbRect = previewScrollbarThumbEl.getBoundingClientRect();
    const maxThumbTop = Math.max(1, trackRect.height - thumbRect.height);
    const maxScrollTop = Math.max(1, previewEl.scrollHeight - previewEl.clientHeight);
    const deltaY = event.clientY - previewScrollbarDragStartY;
    previewEl.scrollTop = previewScrollbarDragStartScrollTop + (deltaY / maxThumbTop) * maxScrollTop;
    updatePreviewScrollbar();
  });

  const stopDragging = () => {
    previewScrollbarDragging = false;
    delete previewScrollbarEl.dataset.dragging;
    schedulePreviewScrollbarFade();
  };

  previewScrollbarEl.addEventListener('pointerup', stopDragging);
  previewScrollbarEl.addEventListener('pointercancel', stopDragging);
  previewScrollbarEl.addEventListener('lostpointercapture', stopDragging);
  previewScrollbarEl.addEventListener('pointerdown', event => {
    if (event.target === previewScrollbarThumbEl) {
      return;
    }

    scrollPreviewToScrollbarPosition(event.clientY);
  });
}

function isEditableRenderedModeActive(): boolean {
  return isPreviewVisible && canUseEditableMarkdown(getCurrentContent());
}

function canUseEditableMarkdown(markdown: string): boolean {
  return isMarkdownDocument && markdown.length <= MAX_EDITABLE_MARKDOWN_CHARS;
}

function captureSourceSelectionBeforeRenderedToggle(): void {
  sourceSelectionBeforeRenderedToggle = editor.getSelection();
}

function restoreSourceSelectionAfterRenderedToggle(): void {
  if (!sourceSelectionBeforeRenderedToggle) {
    return;
  }

  editor.setSelection(sourceSelectionBeforeRenderedToggle);
  editor.revealPositionInCenter({
    lineNumber: sourceSelectionBeforeRenderedToggle.positionLineNumber,
    column: sourceSelectionBeforeRenderedToggle.positionColumn,
  });
  sourceSelectionBeforeRenderedToggle = null;
}

function monacoSelectionToEditableSelection(selection: monaco.Selection | null): EditableMarkdownSelection | null {
  if (!selection) {
    return null;
  }

  const model = editor.getModel();
  if (!model) {
    return null;
  }

  const startOffset = model.getOffsetAt({ lineNumber: selection.selectionStartLineNumber, column: selection.selectionStartColumn });
  const endOffset = model.getOffsetAt({ lineNumber: selection.positionLineNumber, column: selection.positionColumn });
  return { start: startOffset, end: endOffset };
}

function editableSelectionToMonacoSelection(selection: EditableMarkdownSelection | null): monaco.Selection | null {
  if (!selection) {
    return null;
  }

  const model = editor.getModel();
  if (!model) {
    return null;
  }

  const safeStart = Math.max(0, Math.min(model.getValueLength(), selection.start));
  const safeEnd = Math.max(0, Math.min(model.getValueLength(), selection.end));
  const startPosition = model.getPositionAt(safeStart);
  const endPosition = model.getPositionAt(safeEnd);

  return new monaco.Selection(
    startPosition.lineNumber,
    startPosition.column,
    endPosition.lineNumber,
    endPosition.column,
  );
}

function applyRenderedMarkdownTextToSource(markdown: string): void {
  if (markdown === getCurrentContent()) {
    return;
  }

  const model = editor.getModel();
  if (!model) {
    return;
  }

  const fullRange = model.getFullModelRange();
  editor.pushUndoStop();
  editor.executeEdits('markdown-rendered-sync', [{ range: fullRange, text: markdown, forceMoveMarkers: true }]);
  editor.pushUndoStop();
}

function applyRenderedMarkdownViewState(): void {
  editor.updateOptions({ readOnly: isPreviewVisible });

  if (isPreviewVisible) {
    captureSourceSelectionBeforeRenderedToggle();
    editorEl.style.display = 'none';
    splitterEl.style.display = 'none';
    previewEl.style.display = 'block';
    previewEl.style.width = '100%';
    previewScrollbarEl.style.display = 'none';
    previewScrollbarEl.dataset.active = 'false';
    if (isMarkdownDocument) {
      const markdown = getCurrentContent();
      const canEdit = canUseEditableMarkdown(markdown);
      previewEl.dataset.markdownEditMode = canEdit ? 'editable' : 'readonly-large';
      setEditableMarkdownEnabled(canEdit);

      if (canEdit) {
        setEditableMarkdownContent(markdown);
        const editableSelection = monacoSelectionToEditableSelection(sourceSelectionBeforeRenderedToggle);
        if (editableSelection) {
          setEditableMarkdownSelection(editableSelection);
        }
      } else {
        updatePreviewPane(previewEl, markdown);
      }
    } else {
      previewEl.dataset.markdownEditMode = 'readonly';
      setEditableMarkdownEnabled(false);
      updatePreviewPane(previewEl, getCurrentContent());
    }
    updatePreviewScrollbar();
    return;
  }

  if (isMarkdownDocument) {
    const renderedSelection = getEditableMarkdownSelection();
    const monacoSelection = editableSelectionToMonacoSelection(renderedSelection);
    if (monacoSelection) {
      sourceSelectionBeforeRenderedToggle = monacoSelection;
    }
  }

  setEditableMarkdownEnabled(false);
  previewEl.dataset.markdownEditMode = 'readonly';
  previewEl.style.display = 'none';
  previewEl.style.width = '50%';
  previewScrollbarEl.style.display = 'none';
  previewScrollbarEl.dataset.active = 'false';
  splitterEl.style.display = 'none';
  editorEl.style.display = 'block';
  restoreSourceSelectionAfterRenderedToggle();
  editor.focus();
}

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

  if (isPreviewVisible && isMarkdownDocument && previewEl.dataset.markdownEditMode === 'editable' && !canUseEditableMarkdown(content)) {
    setEditableMarkdownEnabled(false);
    previewEl.dataset.markdownEditMode = 'readonly-large';
    updatePreviewPane(previewEl, content);
  }

  if (isPreviewVisible && !isEditableRenderedModeActive()) {
    updatePreviewPane(previewEl, content);
    updatePreviewScrollbar();
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
      isMarkdownDocument = msg.language.toLowerCase() === 'markdown';
      sourceSelectionBeforeRenderedToggle = null;
      setContent(msg.content, msg.language);
      if (isPreviewVisible) {
        if (isEditableRenderedModeActive()) {
          setEditableMarkdownContent(msg.content);
        } else {
          previewEl.dataset.markdownEditMode = isMarkdownDocument && !canUseEditableMarkdown(msg.content)
            ? 'readonly-large'
            : 'readonly';
          updatePreviewPane(previewEl, msg.content);
          updatePreviewScrollbar();
          schedulePreviewScrollbarFade();
        }
      }
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

    case 'markdown:command':
      if (isEditableRenderedModeActive()) {
        applyEditableMarkdownCommand(msg.command, msg.args);
      }
      break;

    case 'preview:toggle':
      isPreviewVisible = msg.visible;
      applyRenderedMarkdownViewState();
      break;

    case 'view:show':
      if (msg.view === 'welcome') {
        isPreviewVisible = false;
        sourceSelectionBeforeRenderedToggle = null;
        setEditableMarkdownEnabled(false);
        previewEl.dataset.markdownEditMode = 'readonly';
        previewEl.style.display = 'none';
        previewEl.style.width = '50%';
        previewScrollbarEl.style.display = 'none';
        previewScrollbarEl.dataset.active = 'false';
        splitterEl.style.display = 'none';
        editorEl.style.display = 'block';
        containerEl.style.display = 'none';
        welcomeEl.style.display   = 'flex';
        mountWelcome(welcomeEl, msg.data);
      } else {
        welcomeEl.style.display   = 'none';
        containerEl.style.display = 'flex';
        applyRenderedMarkdownViewState();
      }
      break;

    case 'editor:request-text':
      bridge.post({ type: 'editor:text:response', content: getCurrentContent() });
      break;
  }
});

// ── Ready signal ──────────────────────────────────────────────────────────
bridge.post({ type: 'editor:ready' });
