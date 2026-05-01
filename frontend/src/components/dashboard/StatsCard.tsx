import Link from "next/link";
import { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

interface StatsCardProps {
  title: string;
  value: number | string;
  icon: LucideIcon;
  color: "blue" | "orange" | "navy" | "green" | "red";
  description?: string;
  href?: string;
}

const COLOR_MAP = {
  blue:   { bg: "bg-clinic-blue-50",  icon: "text-clinic-blue",    border: "border-clinic-blue/15",  ring: "ring-clinic-blue/10"   },
  orange: { bg: "bg-clinic-orange-50", icon: "text-clinic-orange",  border: "border-clinic-orange/15", ring: "ring-clinic-orange/10" },
  navy:   { bg: "bg-clinic-navy-700/5",icon: "text-clinic-navy-700",border: "border-clinic-navy/10",  ring: "ring-clinic-navy/5"    },
  green:  { bg: "bg-green-50",         icon: "text-green-600",      border: "border-green-200",        ring: "ring-green-100"        },
  red:    { bg: "bg-red-50",           icon: "text-red-500",        border: "border-red-200",          ring: "ring-red-100"          },
};

export function StatsCard({ title, value, icon: Icon, color, description, href }: StatsCardProps) {
  const c = COLOR_MAP[color];
  const inner = (
    <>
      <div className={cn("w-12 h-12 rounded-xl flex items-center justify-center flex-shrink-0 ring-1", c.bg, c.ring)}>
        <Icon className={cn("w-6 h-6", c.icon)} />
      </div>
      <div>
        <p className="text-sm text-gray-500 font-medium">{title}</p>
        <p className="text-3xl font-extrabold text-gray-900 mt-0.5">{value}</p>
        {description && <p className="text-xs text-gray-400 mt-1">{description}</p>}
      </div>
    </>
  );

  const cls = cn("bg-white rounded-xl border p-5 flex items-start gap-4 shadow-sm", c.border, href && "hover:shadow-md transition-shadow cursor-pointer");

  return href ? (
    <Link href={href} className={cls}>{inner}</Link>
  ) : (
    <div className={cls}>{inner}</div>
  );
}
