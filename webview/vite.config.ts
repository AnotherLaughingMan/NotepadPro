import { defineConfig } from 'vite';

export default defineConfig({
  // Assets are served at root when hosted via SetVirtualHostNameToFolderMapping
  base: '/',
  build: {
    outDir: '../NotepadPro/wwwroot',
    emptyOutDir: true,
    target: 'esnext',
    minify: 'esbuild',
    rollupOptions: {
      input: 'index.html',
    },
  },
});
