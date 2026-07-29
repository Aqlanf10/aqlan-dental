import { PublicNavbar } from "@/components/public/PublicNavbar";
import { FooterWithSettings } from "./FooterWithSettings";

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen flex flex-col" style={{ backgroundColor: "#F8FAFC" }}>
      <PublicNavbar />
      <main className="flex-1 pt-20">{children}</main>
      <FooterWithSettings />
    </div>
  );
}
