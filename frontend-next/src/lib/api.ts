import type { ProductDiscoveryResponse } from "@/lib/types";

// Browser requests go through the Next.js same-origin rewrite; Server Components call the API origin directly.
const API_BASE = typeof window === "undefined"
  ? (process.env.MERKATO_API_ORIGIN || "https://merkato-bekan25.onrender.com").replace(/\/$/, "") + "/api"
  : (process.env.NEXT_PUBLIC_API_BASE_URL || "/api").replace(/\/$/, "");

export async function getProducts(options: { search?: string; category?: string; location?: string; languageCode?: string } = {}): Promise<ProductDiscoveryResponse> {
  const params = new URLSearchParams({ limit: "24" });
  if (options.search?.trim()) params.set("q", options.search.trim());
  if (options.category && options.category !== "All") params.set("category", options.category);
  if (options.location?.trim()) params.set("location", options.location.trim());
  if (options.languageCode) params.set("languageCode", options.languageCode);
  const response = await fetch(API_BASE + "/v1/discovery/products?" + params.toString(), { headers: { Accept: "application/json" }, cache: "no-store" });
  if (!response.ok) throw new Error("Product discovery failed (" + response.status + ")");
  return response.json() as Promise<ProductDiscoveryResponse>;
}
