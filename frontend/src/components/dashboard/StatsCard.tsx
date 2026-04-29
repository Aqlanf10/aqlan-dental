import { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

interface StatsCardProps {
  title: string;
  value: number | string;
  icon: LucideIcon;
  color: string; // hex color like #3d7ab5
  description?: string;
  href?: string;
}

export function StatsCard({ title, value, icon: Icon, color, description, href }: StatsCardProps) {
  const inner = (
    <>
      <div
        className="w-[44px] h-[44px] rounded-xl flex items-center justify-center flex-shrink-0"
        style={{ background: color + "18" }}
      >
        <Icon className="w-5 h-5" style={{ color }} />
      </div>
      <div className="min-w-0">
        <p className="text-[13px] font-medium text-[#64748b]">{title}</p>
        <p
          className="text-[26px] font-extrabold text-[#0d2137] mt-0.5 leading-none"
          style={{ letterSpacing: "-0.5px" }}
        >
          {value}
        </p>
        {description && <p className="text-[12px] text-[#94a3b8] mt-1">{description}</p>}
      </div>
    </>
  );

  const cls = cn(
    "bg-white rounded-xl p-5 flex items-start gap-4",
    "shadow-[0_1px_3px_rgba(13,33,55,0.06)] border border-[#e8f0f9]",
    href && "hover:shadow-md transition-shadow cursor-pointer"
  );

  if (href) {
    return (
      <a href={href} className={cls}>
        {inner}
      </a>
    );
  }

  return <div className={cls}>{inner}</div>;
}
