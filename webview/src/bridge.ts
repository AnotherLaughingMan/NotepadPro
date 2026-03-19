import type { InboundMessage, OutboundMessage } from './types';

type MessageHandler = (message: InboundMessage) => void;

// Augment Window with the WebView2 postMessage API
declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage(message: string): void;
        addEventListener(type: 'message', handler: (e: MessageEvent) => void): void;
        removeEventListener(type: 'message', handler: (e: MessageEvent) => void): void;
      };
    };
  }
}

class Bridge {
  private readonly handlers: MessageHandler[] = [];
  readonly isWebView2 = typeof window.chrome?.webview !== 'undefined';

  constructor() {
    if (this.isWebView2) {
      window.chrome!.webview!.addEventListener('message', (e: MessageEvent) => {
        try {
          const msg = typeof e.data === 'string'
            ? JSON.parse(e.data) as InboundMessage
            : e.data as InboundMessage;
          this.dispatch(msg);
        } catch {
          // Ignore malformed messages
        }
      });
    } else {
      console.info(
        '%c[NotepadPro] Dev mode — bridge is not connected to a C# host.\n' +
        'Simulate host messages via: window.__bridge.simulate({type: "...", ...})',
        'color: #4ec9b0; font-weight: bold',
      );
    }
  }

  on(handler: MessageHandler): void {
    this.handlers.push(handler);
  }

  post(message: OutboundMessage): void {
    if (this.isWebView2) {
      window.chrome!.webview!.postMessage(JSON.stringify(message));
    } else {
      console.log('%c[→ host]', 'color: #9cdcfe', message);
    }
  }

  /** Simulate an inbound host message — dev/test only. */
  simulate(message: InboundMessage): void {
    console.log('%c[← host (simulated)]', 'color: #ce9178', message);
    this.dispatch(message);
  }

  private dispatch(message: InboundMessage): void {
    for (const handler of this.handlers) {
      handler(message);
    }
  }
}

export const bridge = new Bridge();

// Expose on window for browser console testing
if (typeof window !== 'undefined') {
  (window as unknown as Record<string, unknown>)['__bridge'] = bridge;
}
