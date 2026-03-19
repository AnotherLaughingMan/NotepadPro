import { bridge } from './bridge';
import type { WelcomeData, RecentItem } from './types';

const TEMPLATE = `
<div class="welcome-root">
  <div class="welcome-inner">

    <div class="welcome-col">
      <div class="welcome-brand">
        <span class="welcome-logo">&#xE929;</span>
        <div>
          <h1 class="welcome-title">Notepad Pro</h1>
          <p class="welcome-subtitle">Editing evolved</p>
        </div>
      </div>

      <section class="welcome-section">
        <h2 class="welcome-section-title">Start</h2>
        <ul class="welcome-links">
          <li><button class="welcome-link" data-action="new-file">New File&hellip;</button></li>
          <li><button class="welcome-link" data-action="open-file">Open File&hellip;</button></li>
          <li><button class="welcome-link" data-action="open-folder">Open Folder&hellip;</button></li>
          <li><button class="welcome-link" data-action="open-workspace">Open Workspace&hellip;</button></li>
          <li><button class="welcome-link" data-action="create-workspace">Create New Workspace&hellip;</button></li>
        </ul>
      </section>

      <section class="welcome-section" id="wc-recent-workspaces">
        <h2 class="welcome-section-title">Recent Workspaces</h2>
        <ul class="welcome-links" id="wc-workspaces-list">
          <li class="welcome-empty">No recent workspaces</li>
        </ul>
      </section>
    </div>

    <div class="welcome-col">
      <section class="welcome-section" id="wc-recent-items">
        <h2 class="welcome-section-title">Recent Files &amp; Folders</h2>
        <ul class="welcome-links" id="wc-files-list">
          <li class="welcome-empty">No recent files or folders</li>
        </ul>
      </section>
    </div>

  </div>
</div>
`;

const STYLES = `
.welcome-root {
  width: 100%;
  height: 100%;
  overflow-y: auto;
  display: flex;
  justify-content: center;
  padding: 40px 24px;
  background: var(--editor-bg, #1e1e1e);
  color: var(--editor-fg, #d4d4d4);
  font-family: 'Segoe UI', system-ui, sans-serif;
}

.welcome-inner {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 24px 48px;
  max-width: 900px;
  width: 100%;
}

@media (max-width: 620px) {
  .welcome-inner { grid-template-columns: 1fr; }
}

.welcome-brand {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 32px;
}

.welcome-logo {
  font-family: 'Segoe Fluent Icons', 'Segoe MDL2 Assets';
  font-size: 40px;
  color: var(--editor-fg, #d4d4d4);
  line-height: 1;
}

.welcome-title {
  font-size: 28px;
  font-weight: 300;
  margin: 0;
  color: var(--editor-fg, #d4d4d4);
  letter-spacing: -0.5px;
}

.welcome-subtitle {
  margin: 4px 0 0;
  font-size: 16px;
  color: var(--editor-fg, #888);
  opacity: 0.65;
}

.welcome-section {
  margin-bottom: 28px;
}

.welcome-section-title {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  color: var(--editor-fg, #d4d4d4);
  opacity: 0.55;
  margin: 0 0 10px;
}

.welcome-links {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.welcome-link {
  display: block;
  background: transparent;
  border: none;
  padding: 5px 0;
  cursor: pointer;
  font-size: 14px;
  color: #4da3d4;
  text-align: left;
  border-radius: 3px;
  transition: color 0.1s;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100%;
}

.welcome-link:hover { color: #6ec1f0; text-decoration: underline; }

.welcome-recent-item {
  display: flex;
  flex-direction: column;
  gap: 1px;
  padding: 5px 0;
}

.welcome-recent-name {
  font-size: 14px;
  color: #4da3d4;
  cursor: pointer;
  background: transparent;
  border: none;
  padding: 0;
  text-align: left;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 340px;
}

.welcome-recent-name:hover { color: #6ec1f0; text-decoration: underline; }

.welcome-recent-path {
  font-size: 11px;
  color: var(--editor-fg, #d4d4d4);
  opacity: 0.4;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 340px;
}

.welcome-empty {
  font-size: 13px;
  color: var(--editor-fg, #888);
  opacity: 0.45;
  padding: 4px 0;
}
`;

// Inject styles once
let stylesInjected = false;
function injectStyles(): void {
  if (stylesInjected) return;
  const el = document.createElement('style');
  el.id   = 'welcome-styles';
  el.textContent = STYLES;
  document.head.appendChild(el);
  stylesInjected = true;
}

function buildRecentList(items: RecentItem[], kind: 'file' | 'folder' | 'workspace'): string {
  if (!items.length) return '<li class="welcome-empty">None</li>';

  return items.map(it => `
    <li>
      <div class="welcome-recent-item">
        <button class="welcome-recent-name"
                data-action="open-recent"
                data-path="${escAttr(it.path)}"
                data-kind="${kind}"
                title="${escAttr(it.path)}">
          ${esc(it.displayName)}
        </button>
        <span class="welcome-recent-path" title="${escAttr(it.path)}">${esc(it.path)}</span>
      </div>
    </li>
  `).join('');
}

export function mountWelcome(host: HTMLElement, data?: WelcomeData): void {
  injectStyles();
  host.innerHTML = TEMPLATE;

  if (data) {
    const wsList = host.querySelector('#wc-workspaces-list');
    const filesList = host.querySelector('#wc-files-list');

    if (wsList) wsList.innerHTML = buildRecentList(data.recentWorkspaces, 'workspace');

    // Merge files + folders into one list
    const combined: { items: RecentItem[]; kind: 'file' | 'folder' }[] = [
      ...data.recentFolders.map(i  => ({ items: [i], kind: 'folder' as const })),
      ...data.recentFiles.map(i    => ({ items: [i], kind: 'file'   as const })),
    ];
    if (filesList) {
      if (!combined.length) {
        filesList.innerHTML = '<li class="welcome-empty">No recent files or folders</li>';
      } else {
        filesList.innerHTML = combined.map(({ items, kind }) => buildRecentList(items, kind)).join('');
      }
    }
  }

  // Event delegation — one listener on the welcome root
  host.querySelector('.welcome-root')?.addEventListener('click', e => {
    const el = (e.target as HTMLElement).closest('[data-action]') as HTMLElement | null;
    if (!el) return;
    const action = el.dataset['action'];

    switch (action) {
      case 'new-file':        bridge.post({ type: 'welcome:new-file' });        break;
      case 'open-file':       bridge.post({ type: 'welcome:open-file' });       break;
      case 'open-folder':     bridge.post({ type: 'welcome:open-folder' });     break;
      case 'open-workspace':  bridge.post({ type: 'welcome:open-workspace' });  break;
      case 'create-workspace':bridge.post({ type: 'welcome:create-workspace' });break;
      case 'open-recent': {
        const path = el.dataset['path'] ?? '';
        const kind = (el.dataset['kind'] ?? 'file') as 'file' | 'folder' | 'workspace';
        bridge.post({ type: 'welcome:open-recent', path, kind });
        break;
      }
    }
  });
}

function esc(s: string): string {
  return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

function escAttr(s: string): string {
  return esc(s).replace(/"/g,'&quot;');
}
