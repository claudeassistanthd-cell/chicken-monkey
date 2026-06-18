import { defineConfig } from 'vite'

// The F# sources are compiled to `*.fs.js` next to the `.fs` files by Fable,
// then bundled by Vite. `index.html` is the entry point at the project root.
export default defineConfig({
  root: '.',
  server: {
    port: 5173,
    open: true
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true
  }
})
