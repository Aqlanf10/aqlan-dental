'use client';
import React, { useState } from 'react';
import { 
  Stethoscope, ClipboardList, FileText, PackagePlus, Receipt, 
  ArrowLeftRight, Printer, CalendarPlus, Pill, FileSignature,
  Activity, Scissors, MessageCircle, Image, ScanLine, Wallet,
  CreditCard, ArrowRightLeft, FlaskConical, FolderOpen, KeyRound,
  Clock, ChevronDown, ChevronUp, MoreHorizontal, Plus, Eye
} from 'lucide-react';

interface RibbonGroup {
  label: string;
  items: { icon: any; label: string; color: string; bgColor: string }[];
}

const ribbonGroups: RibbonGroup[] = [
  {
    label: 'إجراءات العيادة',
    items: [
      { icon: Stethoscope, label: 'جلسة علاج', color: 'text-emerald-600', bgColor: 'hover:bg-emerald-50' },
      { icon: ClipboardList, label: 'خطة علاج', color: 'text-sky-600', bgColor: 'hover:bg-sky-50' },
      { icon: CalendarPlus, label: 'موعد جديد', color: 'text-violet-600', bgColor: 'hover:bg-violet-50' },
      { icon: Pill, label: 'وصفة طبية', color: 'text-pink-600', bgColor: 'hover:bg-pink-50' },
    ]
  },
  {
    label: 'المالية V3',
    items: [
      { icon: Receipt, label: 'قبض دفعة', color: 'text-emerald-600', bgColor: 'hover:bg-emerald-50' },
      { icon: ArrowLeftRight, label: 'مرتجع', color: 'text-red-500', bgColor: 'hover:bg-red-50' },
      { icon: FileSignature, label: 'عقد جديد', color: 'text-amber-600', bgColor: 'hover:bg-amber-50' },
      { icon: Wallet, label: 'كشف حساب', color: 'text-sky-600', bgColor: 'hover:bg-sky-50' },
    ]
  },
  {
    label: 'الحالات السريرية',
    items: [
      { icon: Activity, label: 'حالة تقويم', color: 'text-violet-600', bgColor: 'hover:bg-violet-50' },
      { icon: Scissors, label: 'حالة جراحة', color: 'text-rose-600', bgColor: 'hover:bg-rose-50' },
      { icon: Eye, label: 'طب أسنان عام', color: 'text-sky-600', bgColor: 'hover:bg-sky-50' },
    ]
  },
  {
    label: 'المرفقات والسجلات',
    items: [
      { icon: Image, label: 'صور', color: 'text-teal-600', bgColor: 'hover:bg-teal-50' },
      { icon: ScanLine, label: 'أشعة', color: 'text-indigo-600', bgColor: 'hover:bg-indigo-50' },
      { icon: FolderOpen, label: 'مستندات', color: 'text-amber-600', bgColor: 'hover:bg-amber-50' },
      { icon: FlaskConical, label: 'مختبر', color: 'text-cyan-600', bgColor: 'hover:bg-cyan-50' },
    ]
  },
  {
    label: 'طباعة وتواصل',
    items: [
      { icon: Printer, label: 'طباعة', color: 'text-slate-600', bgColor: 'hover:bg-slate-100' },
      { icon: MessageCircle, label: 'رسالة', color: 'text-green-600', bgColor: 'hover:bg-green-50' },
      { icon: KeyRound, label: 'بوابة المريض', color: 'text-orange-600', bgColor: 'hover:bg-orange-50' },
    ]
  },
];

export default function RibbonCommandBar() {
  const [activeAction, setActiveAction] = useState<string | null>(null);
  const [isCollapsed, setIsCollapsed] = useState(false);

  const handleActionClick = (label: string) => {
    setActiveAction(label);
    setTimeout(() => setActiveAction(null), 600);
  };

  return (
    <div className="bg-white border-b border-slate-200 shadow-sm">
      {/* Ribbon Header */}
      <div className="flex items-center justify-between px-4 py-1 bg-slate-50 border-b border-slate-100">
        <div className="flex items-center gap-2">
          <div className="w-1.5 h-1.5 rounded-full bg-sky-500" />
          <span className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">شريط الأوامر</span>
        </div>
        <button 
          onClick={() => setIsCollapsed(!isCollapsed)}
          className="text-slate-400 hover:text-slate-600 transition-colors p-1 rounded hover:bg-slate-200"
        >
          {isCollapsed ? <ChevronDown className="w-3.5 h-3.5" /> : <ChevronUp className="w-3.5 h-3.5" />}
        </button>
      </div>

      {/* Ribbon Content */}
      <div className={`overflow-hidden transition-all duration-400 ${isCollapsed ? 'max-h-0' : 'max-h-[200px]'}`}>
        <div className="p-2 overflow-x-auto flex gap-0.5">
          {ribbonGroups.map((group, gi) => (
            <React.Fragment key={gi}>
              <div className="relative border-l border-slate-200 pr-1 pl-2 pt-5 pb-1 min-w-fit">
                {/* Group Label */}
                <span className="absolute top-1 right-2 text-[9px] text-slate-400 font-bold tracking-wider">
                  {group.label}
                </span>
                {/* Group Buttons */}
                <div className="flex gap-0.5">
                  {group.items.map((item, ii) => {
                    const Icon = item.icon;
                    const isActive = activeAction === item.label;
                    return (
                      <button
                        key={ii}
                        onClick={() => handleActionClick(item.label)}
                        className={`
                          flex flex-col items-center w-[68px] p-1.5 rounded-lg transition-all duration-200 
                          ${item.bgColor} 
                          ${isActive ? 'ring-2 ring-sky-400 ring-offset-1 scale-95 bg-sky-50' : 'hover:scale-105'}
                          active:scale-95
                        `}
                      >
                        <div className={`w-8 h-8 rounded-lg flex items-center justify-center bg-white shadow-sm border border-slate-100 mb-0.5 ${isActive ? 'animate-bounce' : ''}`}>
                          <Icon className={`w-4 h-4 ${item.color}`} />
                        </div>
                        <span className="text-[9px] font-bold text-slate-600 whitespace-nowrap">{item.label}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
              {/* Separator */}
              {gi < ribbonGroups.length - 1 && (
                <div className="w-px bg-slate-200 self-stretch my-2" />
              )}
            </React.Fragment>
          ))}
        </div>
      </div>
    </div>
  );
}
