import Link from "@/lib/nextLinkCompat";
import { LucideIcon } from "lucide-react";
import { cardStyle as baseCardStyle } from "@/components/ui/cardStyles";

interface StatsCardProps {
  title: string;
  value: number | string;
  icon: LucideIcon;
  color: "blue" | "orange" | "navy" | "green" | "red" | "purple";
  description?: string;
  href?: string;
}

/* ZIP reference colors:
   blue: #3d7ab5, orange: #f5922e, purple: #a855f7, green: #22c55e, navy: #0d2137, red: #ef4444
   Icon bg = color + '18' (hex alpha ~9.4%) */
const COLOR_MAP = {
  blue:   { icon: "#3d7ab5", bg: "#3d7ab518" },
  orange: { icon: "#f5922e", bg: "#f5922e18" },
  purple: { icon: "#a855f7", bg: "#a855f718" },
  green:  { icon: "#22c55e", bg: "#22c55e18" },
  navy:   { icon: "#0d2137", bg: "#0d213718" },
  red:    { icon: "#ef4444", bg: "#ef444418" },
};

export function StatsCard({ title, value, icon: Icon, color, description, href }: StatsCardProps) {
  const c = COLOR_MAP[color];

  const inner = (
    <>
      <div className="flex-1">
        <div className="text-[13px] font-medium mb-1.5" style={{ color: "#64748b" }}>{title}</div>
        <div className="text-[26px] font-extrabold leading-none" style={{ color: "#0d2137", letterSpacing: -0.5 }}>{value}</div>
        {description && <div className="text-xs mt-1" style={{ color: "#94a3b8" }}>{description}</div>}
      </div>
      <div
        className="w-11 h-11 rounded-xl flex items-center justify-center flex-shrink-0"
        style={{ background: c.bg }}
      >
        <Icon className="w-[22px] h-[22px]" style={{ color: c.icon }} />
      </div>
    </>
  );

  const cardStyle: React.CSSProperties = {
    ...baseCardStyle,
    display: "flex",
    alignItems: "flex-start",
    justifyContent: "space-between",
  };

  if (href) {
    return (
      <Link href={href} style={cardStyle} className="group hover:shadow-card-hover transition-shadow">
        {inner}
      </Link>
    );
  }

  return (
    <div style={cardStyle} className="card-hover">
      {inner}
    </div>
  );
}
