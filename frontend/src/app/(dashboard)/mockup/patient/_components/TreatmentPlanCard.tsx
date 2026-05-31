'use client';
import React, { useState } from 'react';
import { CheckCircle2, Circle, Clock, Target, TrendingUp } from 'lucide-react';

interface TreatmentStep {
  id: number;
  title: string;
  tooth?: string;
  status: 'completed' | 'current' | 'upcoming';
  date?: string;
}

const treatmentSteps: TreatmentStep[] = [
  { id: 1, title: 'فحص وتشخيص', tooth: '46', status: 'completed', date: '15 مايو' },
  { id: 2, title: 'سحب عصب - الجلسة 1', tooth: '46', status: 'completed', date: '22 مايو' },
  { id: 3, title: 'سحب عصب - الجلسة 2', tooth: '46', status: 'current', date: '1 يونيو' },
  { id: 4, title: 'حشوة دائمة', tooth: '46', status: 'upcoming' },
  { id: 5, title: 'تاج خزفي', tooth: '46', status: 'upcoming' },
];

export default function TreatmentPlanCard() {
  const [expandedStep, setExpandedStep] = useState<number | null>(3);
  const progressPercent = 45;

  return (
    <div className="flex-1 bg-white border border-slate-200 rounded-2xl shadow-sm overflow-hidden hover:shadow-md transition-shadow duration-300">
      {/* Card Header */}
      <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-gradient-to-l from-white to-slate-50/50">
        <div className="flex items-center gap-2">
          <span className="bg-sky-50 text-sky-600 text-[11px] font-bold px-3 py-1 rounded-full border border-sky-100">
            <Target className="inline w-3 h-3 ml-1" /> خطة تقويم
          </span>
        </div>
        <h2 className="text-lg font-black text-slate-800">خطة العلاج الحالية</h2>
      </div>

      {/* Progress Section */}
      <div className="px-6 py-4 border-b border-slate-100">
        <div className="flex items-center justify-between mb-2">
          <span className="text-xs font-bold text-emerald-600 flex items-center gap-1">
            <TrendingUp className="w-3.5 h-3.5" /> {progressPercent}% مكتمل
          </span>
          <span className="text-xs text-slate-400 font-bold">2 من 5 خطوات</span>
        </div>
        <div className="h-2.5 bg-slate-100 rounded-full overflow-hidden">
          <div 
            className="bg-gradient-to-l from-emerald-400 to-emerald-500 h-full rounded-full transition-all duration-1000 ease-out relative"
            style={{ width: `${progressPercent}%` }}
          >
            <div className="absolute inset-0 bg-white/20 animate-pulse rounded-full" />
          </div>
        </div>
      </div>

      {/* Current Step Highlight */}
      <div className="px-6 py-3 bg-gradient-to-l from-sky-50 to-white border-b border-slate-100">
        <div className="flex items-center gap-3 bg-white border border-sky-200 rounded-xl px-4 py-3 shadow-sm">
          <div className="w-8 h-8 bg-sky-100 rounded-lg flex items-center justify-center">
            <Clock className="w-4 h-4 text-sky-600" />
          </div>
          <div className="text-right flex-1">
            <div className="text-sm font-black text-sky-700">الجلسة القادمة</div>
            <div className="text-xs text-slate-500 font-bold">سحب عصب (السن 46) - الجلسة الثانية</div>
          </div>
          <span className="text-[10px] bg-sky-500 text-white font-bold px-2 py-1 rounded-lg">1 يونيو</span>
        </div>
      </div>

      {/* Steps List */}
      <div className="px-6 py-3 max-h-[240px] overflow-y-auto">
        <div className="space-y-1">
          {treatmentSteps.map((step) => (
            <button
              key={step.id}
              onClick={() => setExpandedStep(expandedStep === step.id ? null : step.id)}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl transition-all duration-200 text-right ${
                step.status === 'current' 
                  ? 'bg-sky-50 border border-sky-200' 
                  : step.status === 'completed'
                  ? 'bg-emerald-50/50 hover:bg-emerald-50'
                  : 'hover:bg-slate-50'
              }`}
            >
              {/* Status Icon */}
              {step.status === 'completed' ? (
                <CheckCircle2 className="w-5 h-5 text-emerald-500 flex-shrink-0" />
              ) : step.status === 'current' ? (
                <div className="w-5 h-5 rounded-full border-2 border-sky-500 flex-shrink-0 flex items-center justify-center">
                  <div className="w-2 h-2 bg-sky-500 rounded-full animate-pulse" />
                </div>
              ) : (
                <Circle className="w-5 h-5 text-slate-300 flex-shrink-0" />
              )}
              
              {/* Step Info */}
              <div className="flex-1 min-w-0">
                <div className={`text-sm font-bold ${
                  step.status === 'completed' ? 'text-emerald-700 line-through opacity-70' :
                  step.status === 'current' ? 'text-sky-700' :
                  'text-slate-500'
                }`}>
                  {step.title}
                </div>
                {expandedStep === step.id && step.tooth && (
                  <div className="text-[11px] text-slate-400 mt-0.5">السن {step.tooth}</div>
                )}
              </div>

              {/* Date or Tooth */}
              {step.date && (
                <span className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${
                  step.status === 'completed' ? 'bg-emerald-100 text-emerald-600' :
                  step.status === 'current' ? 'bg-sky-100 text-sky-600' :
                  'bg-slate-100 text-slate-400'
                }`}>
                  {step.date}
                </span>
              )}
              
              {step.tooth && !step.date && (
                <span className="text-[10px] font-bold bg-slate-100 text-slate-400 px-2 py-0.5 rounded-md">
                  #{step.tooth}
                </span>
              )}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
