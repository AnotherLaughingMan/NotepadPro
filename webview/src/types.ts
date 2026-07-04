// ── Inbound messages  (C# host → WebView) ──────────────────────────────────

export type InboundMessage =
  | { type: 'file:open';        path: string; content: string; language: string }
  | { type: 'file:saved' }
  | { type: 'settings:apply';   settings: EditorSettings }
  | { type: 'theme:apply';      theme: string; colors: ThemeColors }
  | { type: 'editor:navigate';  line: number; column?: number }
  | { type: 'editor:bookmarks'; bookmarks: BookmarkMarker[] }
  | { type: 'editor:command';   command: string; args?: unknown }
  | { type: 'markdown:command'; command: string; args?: unknown }
  | { type: 'editor:scrollbarOpacity'; opacity: number }
  | { type: 'preview:toggle';   visible: boolean }
  | { type: 'view:show';        view: 'welcome' | 'editor'; data?: WelcomeData }
  | { type: 'editor:request-text' };

// ── Outbound messages (WebView → C# host) ─────────────────────────────────

export type OutboundMessage =
  | { type: 'editor:ready' }
  | { type: 'file:modified';       isDirty: boolean }
  | { type: 'cursor:changed';      line: number; column: number; selectionLength: number }
  | { type: 'file:save:request';   content: string }
  | { type: 'markdown:content:update'; content: string; sourceMode: 'rendered' }
  | { type: 'status:update';       wordCount: number; language: string; lineCount: number }
  | { type: 'welcome:new-file' }
  | { type: 'welcome:open-file' }
  | { type: 'welcome:open-folder' }
  | { type: 'welcome:open-workspace' }
  | { type: 'welcome:create-workspace' }
  | { type: 'welcome:open-recent'; path: string; kind: 'file' | 'folder' | 'workspace' }
  | { type: 'editor:text:response'; content: string };

// ── Shared data shapes ─────────────────────────────────────────────────────

export interface EditorSettings {
  wordWrap: boolean;
  showLineNumbers: boolean;
  isMinimapVisible: boolean;
  minimapFadeSpeedMs: number;
  autoIndentation: boolean;
  autoBracketing: boolean;
  renderWhitespace: boolean;
  editorFontSize: number;
  indentation: string;
  eol: string;
}

export interface ThemeColors {
  background: string;
  foreground: string;
  selectionBackground: string;
  lineHighlight: string;
  syntaxKeyword: string;
  syntaxString: string;
  syntaxComment: string;
  syntaxNumber: string;
  syntaxType: string;
  syntaxFunction: string;
}

export interface BookmarkMarker {
  line: number;
  state: 'scoped' | 'global' | 'stale';
}

export interface WelcomeData {
  recentFiles:      RecentItem[];
  recentFolders:    RecentItem[];
  recentWorkspaces: RecentItem[];
}

export interface RecentItem {
  displayName: string;
  path: string;
}
