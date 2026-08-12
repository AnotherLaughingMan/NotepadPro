import TurndownService from 'turndown';
import { renderMarkdown } from './markdown-preview';

type MarkdownChangedHandler = (markdown: string) => void;
export interface EditableMarkdownSelection {
  start: number;
  end: number;
}

const turndown = new TurndownService({
  headingStyle: 'atx',
  codeBlockStyle: 'fenced',
  bulletListMarker: '-',
  emDelimiter: '*',
  strongDelimiter: '**',
});

const editableRoot = document.createElement('div');
editableRoot.className = 'notepadpro-md-editable';
editableRoot.contentEditable = 'true';
editableRoot.spellcheck = true;
let editableHost: HTMLElement | null = null;

let isEditableEnabled = false;
let isApplyingExternalContent = false;
let onMarkdownChanged: MarkdownChangedHandler | null = null;
let debouncedSyncTimer = 0;

export function mountEditableMarkdown(host: HTMLElement, onChanged: MarkdownChangedHandler): void {
  onMarkdownChanged = onChanged;
  editableHost = host;

  if (!host.contains(editableRoot)) {
    host.innerHTML = '';
    host.appendChild(editableRoot);
  }

  editableRoot.addEventListener('input', handleEditableInput);
}

export function setEditableMarkdownEnabled(enabled: boolean): void {
  if (!enabled) {
    cancelPendingSync();
  }

  isEditableEnabled = enabled;
  editableRoot.contentEditable = enabled ? 'true' : 'false';
  editableRoot.style.cursor = enabled ? 'text' : 'default';
}

export function setEditableMarkdownContent(markdown: string): void {
  cancelPendingSync();
  if (editableHost && !editableHost.contains(editableRoot)) {
    editableHost.innerHTML = '';
    editableHost.appendChild(editableRoot);
  }

  const previousSelection = getEditableMarkdownSelection();
  isApplyingExternalContent = true;
  editableRoot.innerHTML = renderMarkdown(markdown);
  isApplyingExternalContent = false;

  if (previousSelection) {
    setEditableMarkdownSelection(previousSelection);
  }
}

export function getEditableMarkdownSelection(): EditableMarkdownSelection | null {
  const selection = window.getSelection();
  if (!selection || selection.rangeCount === 0) {
    return null;
  }

  const range = selection.getRangeAt(0);
  if (!editableRoot.contains(range.startContainer) || !editableRoot.contains(range.endContainer)) {
    return null;
  }

  const start = getTextOffset(range.startContainer, range.startOffset);
  const end = getTextOffset(range.endContainer, range.endOffset);
  return { start, end };
}

export function setEditableMarkdownSelection(selection: EditableMarkdownSelection): void {
  if (!isEditableEnabled) {
    return;
  }

  const normalized = normalizeSelection(selection);
  const start = resolveTextLocation(normalized.start);
  const end = resolveTextLocation(normalized.end);

  const range = document.createRange();
  range.setStart(start.node, start.offset);
  range.setEnd(end.node, end.offset);

  const domSelection = window.getSelection();
  if (!domSelection) {
    return;
  }

  editableRoot.focus();
  domSelection.removeAllRanges();
  domSelection.addRange(range);
}

export function applyEditableMarkdownCommand(command: string, args?: unknown): boolean {
  if (!isEditableEnabled) {
    return false;
  }

  editableRoot.focus();

  switch (command) {
    case 'bold':
      wrapSelectionWithTag('strong');
      break;

    case 'italic':
      wrapSelectionWithTag('em');
      break;

    case 'inline-code': {
      const selectedText = window.getSelection()?.toString() || 'code';
      replaceSelectionWithHtml(`<code>${escapeHtml(selectedText)}</code>`);
      break;
    }

    case 'heading': {
      const decrement = extractBooleanOption(args, 'decrement');
      cycleHeadingBlock(decrement);
      break;
    }

    case 'bulleted-list': {
      const convertToNumbered = extractBooleanOption(args, 'convertToNumbered');
      document.execCommand(convertToNumbered ? 'insertOrderedList' : 'insertUnorderedList');
      break;
    }

    case 'numbered-list': {
      const convertToBullets = extractBooleanOption(args, 'convertToBullets');
      document.execCommand(convertToBullets ? 'insertUnorderedList' : 'insertOrderedList');
      break;
    }

    case 'link':
      createOrWrapLink();
      break;

    default:
      return false;
  }

  scheduleSyncToMarkdown();
  return true;
}

function handleEditableInput(): void {
  if (!isEditableEnabled || isApplyingExternalContent) {
    return;
  }

  scheduleSyncToMarkdown();
}

function scheduleSyncToMarkdown(): void {
  cancelPendingSync();
  debouncedSyncTimer = window.setTimeout(() => {
    debouncedSyncTimer = 0;
    if (!isEditableEnabled || isApplyingExternalContent || !onMarkdownChanged) {
      return;
    }

    const markdown = turndown.turndown(editableRoot.innerHTML);
    onMarkdownChanged(markdown);
  }, 140);
}

function cancelPendingSync(): void {
  if (debouncedSyncTimer === 0) {
    return;
  }

  clearTimeout(debouncedSyncTimer);
  debouncedSyncTimer = 0;
}

function cycleHeadingBlock(decrement: boolean): void {
  const selection = window.getSelection();
  if (!selection || selection.rangeCount === 0) {
    return;
  }

  const anchorNode = selection.anchorNode;
  const heading = anchorNode ? findClosestHeading(anchorNode) : null;
  if (!heading) {
    document.execCommand('formatBlock', false, 'h1');
    return;
  }

  const tagName = heading.tagName.toLowerCase();
  const levelText = tagName.replace('h', '');
  const level = Number.parseInt(levelText, 10);
  if (!Number.isFinite(level) || level < 1 || level > 6) {
    document.execCommand('formatBlock', false, 'h1');
    return;
  }

  if (decrement) {
    if (level <= 1) {
      document.execCommand('formatBlock', false, 'p');
      return;
    }

    document.execCommand('formatBlock', false, `h${level - 1}`);
    return;
  }

  if (level >= 6) {
    document.execCommand('formatBlock', false, 'p');
    return;
  }

  document.execCommand('formatBlock', false, `h${level + 1}`);
}

function findClosestHeading(node: Node): HTMLElement | null {
  const element = node.nodeType === Node.ELEMENT_NODE
    ? (node as Element)
    : node.parentElement;

  if (!element) {
    return null;
  }

  return element.closest('h1,h2,h3,h4,h5,h6');
}

function extractBooleanOption(args: unknown, name: string): boolean {
  if (!args || typeof args !== 'object') {
    return false;
  }

  const value = (args as Record<string, unknown>)[name];
  return value === true;
}

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function wrapSelectionWithTag(tagName: 'strong' | 'em'): void {
  const selection = window.getSelection();
  if (!selection || selection.rangeCount === 0) {
    return;
  }

  const range = selection.getRangeAt(0);
  if (!editableRoot.contains(range.startContainer) || !editableRoot.contains(range.endContainer)) {
    return;
  }

  const wrapper = document.createElement(tagName);
  try {
    const contents = range.extractContents();
    wrapper.appendChild(contents);
    range.insertNode(wrapper);
    selectNodeContents(wrapper);
  } catch {
    return;
  }

  scheduleSyncToMarkdown();
}

function replaceSelectionWithHtml(html: string): void {
  const selection = window.getSelection();
  if (!selection || selection.rangeCount === 0) {
    return;
  }

  const range = selection.getRangeAt(0);
  if (!editableRoot.contains(range.startContainer) || !editableRoot.contains(range.endContainer)) {
    return;
  }

  const template = document.createElement('template');
  template.innerHTML = html;

  try {
    range.deleteContents();
    const fragment = template.content;
    const lastNode = fragment.lastChild;
    range.insertNode(fragment);
    if (lastNode) {
      selectNodeContents(lastNode);
    }
  } catch {
    return;
  }

  scheduleSyncToMarkdown();
}

function createOrWrapLink(): void {
  const selection = window.getSelection();
  if (!selection || selection.rangeCount === 0) {
    return;
  }

  const range = selection.getRangeAt(0);
  if (!editableRoot.contains(range.startContainer) || !editableRoot.contains(range.endContainer)) {
    return;
  }

  const link = document.createElement('a');
  link.href = 'https://';

  try {
    if (range.collapsed) {
      link.textContent = 'link';
      range.insertNode(link);
    } else {
      const contents = range.extractContents();
      link.appendChild(contents);
      range.insertNode(link);
    }

    selectNodeContents(link);
  } catch {
    return;
  }

  scheduleSyncToMarkdown();
}

function selectNodeContents(node: Node): void {
  const selection = window.getSelection();
  if (!selection) {
    return;
  }

  const range = document.createRange();
  range.selectNodeContents(node);
  selection.removeAllRanges();
  selection.addRange(range);
}

function normalizeSelection(selection: EditableMarkdownSelection): EditableMarkdownSelection {
  const contentLength = editableRoot.textContent?.length ?? 0;
  const start = Math.max(0, Math.min(contentLength, selection.start));
  const end = Math.max(0, Math.min(contentLength, selection.end));
  if (start <= end) {
    return { start, end };
  }

  return { start: end, end: start };
}

function getTextOffset(node: Node, offset: number): number {
  const range = document.createRange();
  range.selectNodeContents(editableRoot);
  range.setEnd(node, offset);
  return range.toString().length;
}

function resolveTextLocation(targetOffset: number): { node: Node; offset: number } {
  const walker = document.createTreeWalker(editableRoot, NodeFilter.SHOW_TEXT);
  let node = walker.nextNode();
  let remaining = targetOffset;
  let lastTextNode: Node | null = null;

  while (node) {
    lastTextNode = node;
    const textLength = node.textContent?.length ?? 0;
    if (remaining <= textLength) {
      return { node, offset: remaining };
    }

    remaining -= textLength;
    node = walker.nextNode();
  }

  if (lastTextNode) {
    const endOffset = lastTextNode.textContent?.length ?? 0;
    return { node: lastTextNode, offset: endOffset };
  }

  return { node: editableRoot, offset: 0 };
}