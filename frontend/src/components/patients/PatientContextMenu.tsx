"use client";
import { useEffect, useRef } from "react";
import { createPortal } from "react-dom";
import {
  Eye,
  Pencil,
  CalendarPlus,
  MessageSquare,
  Phone,
  Printer,
  Archive,
  RotateCcw,
} from "lucide-react";
import type { PatientListItem } from "@/types/patient";

export interface ContextMenuPosition {
  x: number;
  y: number;
}

interface Props {
  patient: PatientListItem | null;
  position: ContextMenuPosition | null;
  isAdmin?: boolean;
  onClose: () => void;
  onOpen: (id: string) => void;
  onEdit: (id: string) => void;
  onNewAppointment: (id: string) => void;
  onMessage: (id: string) => void;
  onArchive: (patient: PatientListItem) => void;
  onRestore: (patient: PatientListItem) => void;
}

interface MenuItem {
  icon: React.ReactNode;
  label: string;
  action: () => void;
  className?: string;
  divider?: boolean;
  show?: boolean;
}

export function PatientContextMenu({
  patient,
  position,
  isAdmin,
  onClose,
  onOpen,
  onEdit,
  onNewAppointment,
  onMessage,
  onArchive,
  onRestore,
}: Props) {
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handle = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("mousedown", handle);
    document.addEventListener("keydown", handleKey);
    return () => {
      document.removeEventListener("mousedown", handle);
      document.removeEventListener("keydown", handleKey);
    };
  }, [onClose]);

  if (!patient || !position) return null;

  // Adjust position to avoid overflow
  const menuWidth = 220;
  const menuHeight = 310;
  const vw = window.innerWidth;
  const vh = window.innerHeight;

  let x = position.x;
  let y = position.y;
  if (x + menuWidth > vw) x = vw - menuWidth - 8;
  if (y + menuHeight > vh) y = vh - menuHeight - 8;
  if (x < 8) x = 8;
  if (y < 8) y = 8;

  const isArchived = !patient.isActive;

  const items: MenuItem[] = [
    {
      icon: <Eye className="w-4 h-4" />,
      label: "عرض الملف",
      action: () => { onOpen(patient.id); onClose(); },
    },
    {
      icon: <Pencil className="w-4 h-4" />,
      label: "تعديل البيانات",
      action: () => { onEdit(patient.id); onClose(); },
      show: !isArchived,
    },
    {
      icon: <CalendarPlus className="w-4 h-4" />,
      label: "موعد جديد",
      action: () => { onNewAppointment(patient.id); onClose(); },
      divider: true,
      show: !isArchived,
    },
    {
      icon: <MessageSquare className="w-4 h-4" />,
      label: "رسالة داخلية",
      action: () => { onMessage(patient.id); onClose(); },
      show: !isArchived,
    },
    {
      icon: <Phone className="w-4 h-4" />,
      label: "واتساب",
      action: () => {
        const phone = patient.phone?.replace(/\D/g, "");
        if (phone) window.open(`https://wa.me/${phone}`, "_blank");
        onClose();
      },
      show: !!patient.phone && !isArchived,
    },
    {
      icon: <Printer className="w-4 h-4" />,
      label: "طباعة ملف المريض",
      action: () => { onOpen(patient.id); onClose(); },
      divider: true,
      show: !isArchived,
    },
    {
      icon: <Archive className="w-4 h-4" />,
      label: "أرشفة المريض",
      action: () => { onArchive(patient); onClose(); },
      className: "text-red-600 hover:bg-red-50",
      show: !isArchived && isAdmin,
    },
    {
      icon: <RotateCcw className="w-4 h-4" />,
      label: "استعادة المريض",
      action: () => { onRestore(patient); onClose(); },
      className: "text-green-700 hover:bg-green-50",
      show: isArchived && isAdmin,
    },
  ].filter((item) => item.show !== false);

  const menu = (
    <div
      ref={menuRef}
      style={{ position: "fixed", top: y, left: x, zIndex: 9999, minWidth: menuWidth }}
      className="bg-white rounded-xl shadow-2xl border border-[#e8f0f9] py-1 text-sm"
      dir="rtl"
    >
      {/* Header */}
      <div className="px-3 py-2 border-b border-gray-100">
        <p className="font-semibold text-gray-800 truncate">{patient.fullName}</p>
        <p className="text-xs text-gray-400">{patient.patientNumber}</p>
      </div>
      {/* Items */}
      {items.map((item, i) => (
        <div key={i}>
          {item.divider && i > 0 && <div className="h-px bg-[#f1f5f9] my-1" />}
          <button
            onClick={item.action}
            className={`w-full flex items-center gap-3 px-3 py-2 hover:bg-gray-50 transition text-gray-700 ${item.className ?? ""}`}
          >
            <span className="flex-shrink-0 text-gray-400">{item.icon}</span>
            {item.label}
          </button>
        </div>
      ))}
    </div>
  );

  if (typeof document === "undefined") return null;
  return createPortal(menu, document.body);
}
