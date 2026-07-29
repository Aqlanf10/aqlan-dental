/** @type {import('next').NextConfig} */
const nextConfig = {
  output: "standalone",
  // BUILD-01 FIX: Removed ignoreBuildErrors and ignoreDuringBuilds
  // TypeScript and ESLint errors must be fixed properly, not hidden
  images: {
    remotePatterns: [
      { protocol: "https", hostname: "*.r2.cloudflarestorage.com" },
      { protocol: "https", hostname: "*.s3.amazonaws.com" },
    ],
  },
  async rewrites() {
    const backendUrl = process.env.BACKEND_URL ?? "http://localhost:5000";
    return [
      { source: "/api/:path*", destination: `${backendUrl}/api/:path*` },
      // SignalR WebSocket hub proxy — required for real-time messaging
      { source: "/hubs/:path*", destination: `${backendUrl}/hubs/:path*` },
      // NAV-CEPH-FIX (Part 2): /uploads/* proxy — keeps image requests same-origin in production
      // (Vercel frontend → Railway backend). The backend /uploads/* auth middleware reads the
      // `aqlan_access_token` cookie (SameSite=Strict), which browsers only send on same-origin
      // requests. Without this rewrite, <img src="https://backend.../uploads/x.png"> is
      // cross-origin → the SameSite=Strict cookie is NOT sent → backend returns 401 → ceph X-rays
      // and clinical photos fail to load. With the rewrite, the request goes through the Next.js
      // origin and the cookie travels normally.
      { source: "/uploads/:path*", destination: `${backendUrl}/uploads/:path*` },
      // Health check endpoint proxy — Railway healthcheck needs this
      { source: "/health", destination: `${backendUrl}/health` },
    ];
  },
};
export default nextConfig;
