import type { Metadata } from "next";
import localFont from "next/font/local";
import { Providers } from "@/providers";
import "./globals.css";

const tajawal = localFont({
  src: [
    { path: "./fonts/Tajawal-Regular.woff2", weight: "400", style: "normal" },
    { path: "./fonts/Tajawal-Medium.woff2", weight: "500", style: "normal" },
    { path: "./fonts/Tajawal-Bold.woff2", weight: "700", style: "normal" },
    { path: "./fonts/Tajawal-ExtraBold.woff2", weight: "800", style: "normal" },
  ],
  variable: "--font-tajawal",
  display: "swap",
  preload: true,
  fallback: ["Arial", "Tahoma", "sans-serif"],
});

export const metadata: Metadata = {
  title: "مركز د. عقلان الكامل لطب وتقويم الأسنان",
  description: "نظام إدارة مركز د. عقلان الكامل لطب وتقويم الأسنان — تعز، اليمن",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="ar" dir="rtl" className={tajawal.variable} suppressHydrationWarning>
      <body className="font-sans antialiased bg-bg-main text-text-primary">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
