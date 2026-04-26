import Link from "next/link";
import { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

interface StatsCardProps {
  title: string;
  value: number | string;
  icon: LucideIcon;
  color: "teal" | "gold" | "purple" | "green" | "red";
  description?: string;
  href?: string;
}

const COLOR_MAP = {
  teal:   { bg: "bg-clinic-teal-light",  icon: "text-clinic-teal",    border: "border-clinic-teal/20"  },
  gold:   { bg: "bg-clinic-gold-light",  icon: "text-clinic-gold",    border: "border-clinic-gold/20"  },
  purple: { bg: "bg-purple-50",          icon: "text-purple-600",      border: "border-purple-200"       },
  green:  { bg: "bg-green-50",           icon: "text-green-600",       border: "border-green-200"        },
  red:    { bg: "bg-red-50",             icon: "text-red-500",         border: "border-red-200"          },
};

export function StatsCard({ title, value, icon: Icon, color, description, href }: StatsCardProps) {
  const c = COLOR_MAP[color];
  const inner = (
    <>
      <div className={cn("w-12 h-12 rounded-xl flex items-center justify-center flex-shrink-0", c.bg)}>
        <Icon className={cn("w-6 h-6", c.icon)} />
      </div>
      <div>
        <p className="text-sm text-gray-500 font-medium">{title}</p>
        <p className="text-3xl font-extrabold text-gray-900 mt-0.5">{value}</p>
        {description && <p className="text-xs text-gray-400 mt-1">{description}</p>}
      </div>
    </>
  );

  const cls = cn("bg-white rounded-xl border p-5 flex items-start gap-4 shadow-sm", c.border, href && "hover:shadow-md transition-shadow");

  return href ? (
    <Link href={href} className={cls}>{inner}</Link>
  ) : (
    <div className={cls}>{inner}</div>
  );
}
