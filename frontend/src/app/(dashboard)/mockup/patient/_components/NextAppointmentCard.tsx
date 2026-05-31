'use client';
import React, { useState } from 'react';
import { Clock, MapPin, Bell, CheckCircle2, Phone, Navigation } from 'lucide-react';

export default function NextAppointmentCard() {
  const [isReminded, setIsReminded] = useState(false);

  return (
    <div 
      className="min-w-[280px] max-w-[320px] rounded-2xl overflow-hidden shadow-lg hover:shadow-xl transition-all duration-500"
    >
      {/* Main Card */}
      <div className="bg-gradient-to-b from-sky-400 via-sky-500 to-blue-600 text-white p-5 relative overflow-hidden">
        {/* Decorative circles */}
        <div className="absolute -top-10 -left-10 w-32 h-32 bg-white/5 rounded-full" />
        <div className="absolute -bottom-8 -right-8 w-24 h-24 bg-white/5 rounded-full" />
        
        {/* Header */}
        <div className="relative z-10">
          <div className="flex items-center gap-2 mb-4">
            <div className="w-2 h-2 rounded-full bg-white/80 animate-pulse" />
            <span className="text-xs font-bold opacity-80 uppercase tracking-wider">الموعد القادم</span>
          </div>
          
          {/* Date & Time */}
          <div className="mb-5">
            <div className="text-2xl font-black tracking-tight">الإثنين، 1 يونيو</div>
            <div className="flex items-center gap-2 mt-1.5">
              <Clock className="w-4 h-4 opacity-70" />
              <span className="text-base font-bold">10:30 صباحاً</span>
              <span className="text-[10px] bg-white/20 px-2 py-0.5 rounded-full font-bold">بعد 3 أيام</span>
            </div>
          </div>
        </div>
      </div>

      {/* Doctor & Clinic Info */}
      <div className="bg-gradient-to-b from-blue-600 to-blue-700 text-white p-4">
        <div className="bg-white/10 backdrop-blur-sm rounded-xl p-3.5">
          <div className="flex items-center gap-3 mb-2">
            <div className="w-9 h-9 bg-white/20 rounded-xl flex items-center justify-center text-sm font-black">
              عغ
            </div>
            <div className="text-right">
              <div className="text-sm font-bold">د. عائشة غازي</div>
              <div className="text-[11px] opacity-70 flex items-center gap-1 justify-end">
                <MapPin className="w-3 h-3" /> عيادة سحب العصب
              </div>
            </div>
          </div>
          
          {/* Treatment preview */}
          <div className="border-t border-white/10 pt-2 mt-2">
            <div className="text-[11px] opacity-60 font-bold mb-1">الإجراء</div>
            <div className="text-xs font-bold bg-white/10 rounded-lg px-3 py-1.5">
              سحب عصب السن 46 - الجلسة الثانية
            </div>
          </div>
        </div>
      </div>

      {/* Action Buttons */}
      <div className="bg-blue-700 p-3 flex gap-2">
        <button 
          onClick={() => setIsReminded(!isReminded)}
          className={`flex-1 flex items-center justify-center gap-1.5 py-2 rounded-lg text-xs font-bold transition-all duration-300 ${
            isReminded 
              ? 'bg-emerald-500 text-white' 
              : 'bg-white/10 text-white/90 hover:bg-white/20'
          }`}
        >
          {isReminded ? <CheckCircle2 className="w-3.5 h-3.5" /> : <Bell className="w-3.5 h-3.5" />}
          {isReminded ? 'تم التذكير' : 'تذكير'}
        </button>
        <button className="flex items-center justify-center gap-1.5 py-2 px-3 rounded-lg text-xs font-bold bg-white/10 text-white/90 hover:bg-white/20 transition-all">
          <Phone className="w-3.5 h-3.5" /> اتصال
        </button>
        <button className="flex items-center justify-center py-2 px-3 rounded-lg text-xs font-bold bg-white/10 text-white/90 hover:bg-white/20 transition-all">
          <Navigation className="w-3.5 h-3.5" />
        </button>
      </div>
    </div>
  );
}
