'use client';
import React, { useState } from 'react';
import { Droplet, AlertTriangle, Coins, Copy, Check } from 'lucide-react';

interface PatientBannerProps {
  patient: {
    name: string;
    id: string;
    age: number;
    phone: string;
    bloodType: string;
    alerts: string[];
    balance: number;
  };
}

export default function PatientBanner({ patient }: PatientBannerProps) {
  const [phoneCopied, setPhoneCopied] = useState(false);
  const [isHovered, setIsHovered] = useState(false);

  const copyPhone = () => {
    navigator.clipboard?.writeText(patient.phone);
    setPhoneCopied(true);
    setTimeout(() => setPhoneCopied(false), 2000);
  };

  return (
    <div 
      className="w-full bg-white border-b border-slate-200 transition-all duration-300"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      {/* Top accent line - Microsoft Fluent style */}
      <div className="h-1 bg-gradient-to-l from-sky-500 via-blue-600 to-indigo-600" />
      
      <div className="px-6 py-4 flex flex-col md:flex-row justify-between items-start gap-4">
        {/* Right side - Alerts & Balance */}
        <div className="flex items-center gap-3 order-2 md:order-1">
          {/* Financial Balance Card */}
          <div className="group relative bg-gradient-to-br from-orange-50 to-amber-50 border border-orange-200 rounded-xl p-3 text-center min-w-[140px] shadow-sm hover:shadow-md transition-all duration-300 cursor-pointer hover:scale-[1.02]">
            <span className="block text-[10px] text-orange-600 font-bold uppercase mb-1 tracking-wider">
              <Coins className="inline w-3 h-3 ml-1" /> الرصيد المالي
            </span>
            <span className="text-lg font-black text-orange-700" dir="ltr">
              {patient.balance.toLocaleString()} YER
            </span>
            <div className="absolute -bottom-1 left-1/2 -translate-x-1/2 w-3 h-3 bg-orange-200 rotate-45 border-b border-r border-orange-300" />
          </div>

          {/* Medical Alerts Card */}
          <div className="bg-gradient-to-br from-red-50 to-rose-50 border border-red-100 p-2.5 rounded-xl min-w-[160px] hover:shadow-md transition-all duration-300 cursor-pointer hover:scale-[1.02]">
            <span className="block text-[10px] text-red-500 font-bold mb-1.5 tracking-wider">
              <AlertTriangle className="inline w-3 h-3 ml-1" /> تنبيهات طبية
            </span>
            <div className="flex flex-wrap gap-1">
              {patient.alerts.map((a: string, i: number) => (
                <span 
                  key={i} 
                  className="bg-red-500 text-white text-[11px] font-bold px-2 py-0.5 rounded-md hover:bg-red-600 transition-colors"
                >
                  {a}
                </span>
              ))}
            </div>
          </div>
        </div>

        {/* Left side - Patient Info */}
        <div className="flex items-center gap-5 mr-auto order-1 md:order-2">
          <div className="text-right">
            {/* Name + Status */}
            <div className="flex items-center justify-end gap-2 mb-1">
              <span className="bg-emerald-500 text-white text-[10px] font-bold px-2 py-0.5 rounded-full shadow-sm animate-pulse">
                نشط
              </span>
              <h1 className="text-2xl font-black text-slate-800 tracking-tight">{patient.name}</h1>
            </div>
            {/* Meta info */}
            <div className="text-xs text-slate-500 font-bold flex items-center gap-2 mt-1.5 justify-end">
              <span className="bg-slate-100 px-2 py-0.5 rounded-md flex items-center gap-1">
                <Droplet className="w-3 h-3 text-red-400" /> {patient.bloodType}
              </span>
              <span className="text-slate-300">|</span>
              <button 
                onClick={copyPhone}
                className="bg-slate-100 px-2 py-0.5 rounded-md flex items-center gap-1 hover:bg-sky-50 transition-colors cursor-pointer"
              >
                {phoneCopied ? <Check className="w-3 h-3 text-emerald-500" /> : <Copy className="w-3 h-3 text-slate-400" />}
                {patient.phone}
              </button>
              <span className="text-slate-300">|</span>
              <span>{patient.age} سنة</span>
              <span className="text-slate-300">|</span>
              <span className="text-slate-400">#{patient.id}</span>
            </div>
          </div>
          
          {/* Avatar */}
          <div className="relative">
            <div className="w-14 h-14 bg-gradient-to-br from-sky-500 to-indigo-600 rounded-2xl flex items-center justify-center font-black text-white text-lg shadow-lg shadow-sky-200/50">
              {patient.name.split(' ').map((n: string) => n[0]).join('').slice(0, 2)}
            </div>
            {/* Online indicator */}
            <div className="absolute -bottom-0.5 -left-0.5 w-4 h-4 bg-emerald-400 border-2 border-white rounded-full" />
          </div>
        </div>
      </div>

      {/* Quick Stats Bar - appears on hover */}
      <div className={`overflow-hidden transition-all duration-500 ${isHovered ? 'max-h-16 opacity-100' : 'max-h-0 opacity-0'}`}>
        <div className="px-6 pb-3 grid grid-cols-6 gap-2">
          {[
            { label: 'المواعيد', value: '3', bgClass: 'bg-sky-50 border-sky-100', textClass: 'text-sky-700' },
            { label: 'المكتملة', value: '12', bgClass: 'bg-emerald-50 border-emerald-100', textClass: 'text-emerald-700' },
            { label: 'التقويم النشط', value: '1', bgClass: 'bg-violet-50 border-violet-100', textClass: 'text-violet-700' },
            { label: 'المدفوع', value: '45,000', bgClass: 'bg-green-50 border-green-100', textClass: 'text-green-700' },
            { label: 'المستحق', value: '25,000', bgClass: 'bg-orange-50 border-orange-100', textClass: 'text-orange-700' },
            { label: 'الوصفات', value: '5', bgClass: 'bg-blue-50 border-blue-100', textClass: 'text-blue-700' },
          ].map((stat, i) => (
            <div key={i} className={`${stat.bgClass} rounded-lg px-3 py-1.5 text-center border ${stat.bgClass.split(' ')[1]}`}>
              <div className={`${stat.textClass} text-sm font-black`}>{stat.value}</div>
              <div className="text-[9px] text-slate-500 font-bold">{stat.label}</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
