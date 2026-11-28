import type { Metadata } from "next";
import "./globals.css";
export const metadata: Metadata = { title: "MERKATO | Ethiopia's trusted marketplace", description: "Discover products and trusted merchants across Ethiopia." };
export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) { return <html lang="en"><body>{children}</body></html>; }
