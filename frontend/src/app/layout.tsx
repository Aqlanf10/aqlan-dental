import type { Metadata, Viewport } from "next";
import localFont from "next/font/local";
import { Providers } from "@/providers";
import { RecaptchaProvider } from "@/lib/recaptcha";
import { ServiceWorkerRegistrar } from "@/components/pwa/ServiceWorkerRegistrar";
import "./globals.css";

const tajawal = localFont({
  src: [
    { path: "./fonts/Tajawal-Regular.woff2",   weight: "400", style: "normal" },
    { path: "./fonts/Tajawal-Medium.woff2",    weight: "500", style: "normal" },
    { path: "./fonts/Tajawal-Bold.woff2",      weight: "700", style: "normal" },
    { path: "./fonts/Tajawal-ExtraBold.woff2", weight: "800", style: "normal" },
  ],
  variable: "--font-tajawal",
  display: "swap",
  preload: true,
  fallback: ["Arial", "Tahoma", "sans-serif"],
});

export const metadata: Metadata = {
  title: "Aqlan Dental Pro — مركز د. عقلان الكامل",
  description: "نظام إدارة مركز د. عقلان الكامل لطب وتقويم الأسنان — تعز، اليمن",
  icons: {
    icon: "/logo.png",
    apple: "/logo-icon.png",
  },
  // Makes the mobile screen installable on Android from Chrome's "add to home screen".
  manifest: "/manifest.webmanifest",
  appleWebApp: {
    capable: true,
    title: "عقلان",
    statusBarStyle: "default",
  },
};

export const viewport: Viewport = {
  themeColor: "#0e7490",
  // viewport-fit=cover so the fixed bottom bar on /m can sit above the phone's home indicator
  // instead of underneath it.
  viewportFit: "cover",
  width: "device-width",
  initialScale: 1,
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html dir="rtl" lang="ar" className={tajawal.variable}>
      <body className="font-sans antialiased">
        <RecaptchaProvider>
          <Providers>{children}</Providers>
        </RecaptchaProvider>
        <ServiceWorkerRegistrar />
      </body>
    </html>
  );
}
