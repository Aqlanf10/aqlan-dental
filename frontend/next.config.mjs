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
    ];
  },
};
export default nextConfig;
