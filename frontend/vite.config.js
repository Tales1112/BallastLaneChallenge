import { defineConfig } from "vite";
export default defineConfig({
  base: "/app/",
  build: {
    outDir: "../src/TaskFlow.Web/wwwroot/app",
    emptyOutDir: true,
    rollupOptions: {
      output: {
        entryFileNames: "assets/app.js",
        chunkFileNames: "assets/[name]-[hash].js",
        assetFileNames: "assets/app.[ext]",
      },
    },
  },
  server: { proxy: { "/api": "http://localhost:5180" } },
});
