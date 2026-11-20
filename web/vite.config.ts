import { defineConfig } from 'vite';

export default defineConfig({
  base: '/wifi-signal-positioning-prototype/',
  build: {
    rollupOptions: { output: { manualChunks: { three: ['three'] } } },
  },
});
