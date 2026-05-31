import type { Metadata } from "next";
import { PublicNavbar } from "@/components/public/PublicNavbar";
import { FooterWithSettings } from "./FooterWithSettings";

export const metadata: Metadata = {
  title: "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان — تعز، اليمن",
  description:
    "مركز متخصص في تقويم وزراعة وتجميل الأسنان في تعز، اليمن. خبرة أكاديمية وسريرية، تشخيص دقيق، وخطط علاج واضحة.",
  icons: {
    icon: "/favicon.ico",
    apple: "/favicon.png",
  },
};

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen flex flex-col" style={{ backgroundColor: "#F8FAFC" }}>
      <PublicNavbar />

      <main className="flex-1 pt-20">{children}</main>

      <FooterWithSettings />
    </div>
  );
}
