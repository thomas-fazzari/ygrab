import { resolve } from "node:path";
import { env } from "node:process";

import solid from "@solidjs/vite-plugin";
import { defineConfig } from "vite";

export default defineConfig({
  resolve: { alias: { "@": resolve(import.meta.dirname, "src") } },
  plugins: [solid()],
  build: {
    outDir: resolve(import.meta.dirname, "../src/YGrab.Host/wwwroot"),
    emptyOutDir: true,
    target: "esnext",
  },
  server: {
    host: "127.0.0.1",
    // Keeping the browser's Host header lets the host's OriginFilter accept the requests
    proxy: {
      "/api": { target: env["YGRAB_HOST_URL"] ?? "http://127.0.0.1:5000", changeOrigin: false },
    },
  },
});
