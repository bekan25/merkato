export interface MerkatoProduct { id: number; sellerId: number; sellerVerified?: boolean; name: string; description?: string | null; languageCode?: string; priceEtb: number; location: string; category: string; emoji?: string; imageUrl?: string | null; stockQuantity?: number; }
export interface ProductDiscoveryResponse { items: MerkatoProduct[]; count: number; limit: number; }
export type MerkatoRole = "BUYER" | "MERCHANT" | "COURIER" | "INSPECTOR" | "ARBITER" | "ADMIN";
export interface AuthUser { userId: number; phoneNumber: string; fullName: string; role: MerkatoRole; trustScore: number; isFaydaVerified: boolean; }
