import type { NextConfig } from "next";

const apiOrigin = (process.env.MERKATO_API_ORIGIN || "https://merkato-bekan25.onrender.com").replace(/\/$/, "");
const nextConfig: NextConfig = {
  poweredByHeader: false,
  async rewrites() {
    return [{ source: "/api/:path*", destination: apiOrigin + "/api/:path*" }];
  },
};

export default nextConfig;
