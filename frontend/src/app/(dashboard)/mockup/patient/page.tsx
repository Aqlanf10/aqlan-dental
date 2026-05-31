'use client';
import React, { useState } from 'react';
import PatientBanner from './_components/PatientBanner';
import RibbonCommandBar from './_components/RibbonCommandBar';
import NextAppointmentCard from './_components/NextAppointmentCard';
import TreatmentPlanCard from './_components/TreatmentPlanCard';
import PivotTabs from './_components/PivotTabs';
import { 
  LayoutDashboard, User, FileText, Stethoscope, Calendar, ClipboardList, 
  Activity, Grid3x3, Scissors, Image, ScanLine, Pill, ArrowRightLeft, 
  FlaskConical, FolderOpen, Wallet, FileSignature, CreditCard, MessageCircle, 
  Clock, KeyRound, Sparkles, ArrowRight
} from 'lucide-react';

const mockPatient = {
  name: 'أحمد محمد عبدالله',
  id: '2605',
  age: 35,
  phone: '777123456',
  bloodType: 'O+',
  alerts: ['سكري', 'حساسية بنسلين'],
  balance: 25000,
};

const allTabs = [
  { key: 'overview', label: 'نظرة عامة', icon: LayoutDashboard },
  { key: 'info', label: 'المعلومات الأساسية', icon: User, group: 'عام' },
  { key: 'medical', label: 'التاريخ الطبي', icon: FileText, group: 'عام' },
  { key: 'dental', label: 'التاريخ السني', icon: Stethoscope, group: 'عام' },
  { key: 'appointments', label: 'المواعيد', icon: Calendar, group: 'سريري' },
  { key: 'visits', label: 'الزيارات', icon: ClipboardList, group: 'سريري' },
  { key: 'orthodontics', label: 'التقويم', icon: Activity, group: 'سريري' },
  { key: 'general', label: 'طب الأسنان العام', icon: Grid3x3, group: 'سريري' },
  { key: 'surgery', label: 'الجراحة', icon: Scissors, group: 'سريري' },
  { key: 'treatment-plan', label: 'خطة العلاج', icon: ClipboardList, group: 'سريري' },
  { key: 'photos', label: 'الصور', icon: Image, group: 'سجلات' },
  { key: 'radiographs', label: 'الأشعة', icon: ScanLine, group: 'سجلات' },
  { key: 'prescriptions', label: 'الوصفات', icon: Pill, group: 'سجلات' },
  { key: 'referrals', label: 'الإحالات', icon: ArrowRightLeft, group: 'سجلات' },
  { key: 'lab-orders', label: 'طلبات المختبر', icon: FlaskConical, group: 'سجلات' },
  { key: 'documents', label: 'المستندات', icon: FolderOpen, group: 'سجلات' },
  { key: 'finance', label: 'المالية', icon: Wallet, group: 'مالي' },
  { key: 'contracts', label: 'العقود', icon: FileSignature, group: 'مالي' },
  { key: 'payments', label: 'المدفوعات', icon: CreditCard, group: 'مالي' },
  { key: 'messages', label: 'الرسائل', icon: MessageCircle, group: 'تواصل' },
  { key: 'timeline', label: 'السجل الزمني', icon: Clock, group: 'تواصل' },
  { key: 'portal-access', label: 'بوابة المريض', icon: KeyRound, group: 'بوابة' },
];

// Mock content for each tab
function TabContent({ tabKey }: { tabKey: string }) {
  const contentMap: Record<string, { title: string; description: string; items: string[] }> = {
    'overview': { title: 'نظرة عامة على المريض', description: 'ملخص شامل لحالة المريض يشمل المواعيد القادمة، والزيارات الأخيرة، والحالة المالية، والتنبيهات الهامة.', items: ['آخر زيارة: 22 مايو 2026', 'الرصيد المستحق: 25,000 YER', '3 مواعيد قادمة', 'خطة علاج نشطة: سحب عصب السن 46'] },
    'info': { title: 'المعلومات الأساسية', description: 'البيانات الشخصية للمريض تشمل الاسم، تاريخ الميلاد، رقم الهاتف، العنوان، ومعلومات الاتصال في حالات الطوارئ.', items: ['الاسم: أحمد محمد عبدالله', 'تاريخ الميلاد: 1991-03-15', 'الهاتف: 777123456', 'العنوان: صنعاء - شارع الزبيري'] },
    'medical': { title: 'التاريخ الطبي', description: 'سجل الحالات المزمنة والحساسية والأدوية الحالية وأي حالات طبية يجب مراعاتها أثناء العلاج.', items: ['سكري نوع 2', 'حساسية بنسلين', 'ضغط دم مرتفع', 'لا توجد عمليات سابقة'] },
    'appointments': { title: 'المواعيد', description: 'قائمة بجميع المواعيد المجدولة والسابقة مع حالتها وتفاصيلها.', items: ['1 يونيو - سحب عصب الجلسة 2', '8 يونيو - متابعة', '15 يونيو - حشوة دائمة'] },
    'visits': { title: 'الزيارات', description: 'سجل الزيارات والإجراءات التي تمت في كل زيارة مع التشخيصات والملاحظات.', items: ['22 مايو - سحب عصب الجلسة 1', '15 مايو - فحص وتشخيص', '10 أبريل - تنظيف أسنان'] },
    'orthodontics': { title: 'حالات التقويم', description: 'إدارة حالات التقويم مع تتبع المراحل والتعديلات والمدة المتوقعة.', items: ['تقويم علوي - مرحلة التوسيط', 'بدء العلاج: يناير 2026', 'المدة المتوقعة: 18 شهر'] },
    'treatment-plan': { title: 'خطة العلاج', description: 'خطة العلاج التفصيلية مع مراحل التنفيذ والنسب والتكلفة الإجمالية.', items: ['سحب عصب السن 46 - قيد التنفيذ', 'حشوة دائمة السن 46 - قادم', 'تاج خزفي السن 46 - مخطط'] },
    'finance': { title: 'المالية', description: 'ملخص الحالة المالية للمريض من فواتير ومدفوعات ورصيد مستحق وعقود.', items: ['إجمالي الفواتير: 70,000 YER', 'إجمالي المدفوع: 45,000 YER', 'الرصيد المستحق: 25,000 YER'] },
    'payments': { title: 'المدفوعات', description: 'سجل جميع المدفوعات والإيصالات مع تفاصيل كل عملية دفع.', items: ['22 مايو - 15,000 YER نقداً', '15 مايو - 10,000 YER تحويل', '10 أبريل - 20,000 YER نقداً'] },
  };

  const content = contentMap[tabKey] || { 
    title: allTabs.find(t => t.key === tabKey)?.label || tabKey, 
    description: 'محتوى هذا القسم سيتم تنفيذه في المرحلة القادمة من التطوير.',
    items: ['بيانات تجريبية - سيتم استبدالها بالبيانات الحقيقية', 'التصميم تفاعلي ويمكنك تجربة جميع الأزرار والتبويبات']
  };

  return (
    <div className="p-6 animate-fadeIn">
      <div className="bg-white border border-slate-200 rounded-2xl overflow-hidden shadow-sm">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-100 bg-gradient-to-l from-white to-slate-50/50 flex items-center gap-3">
          <div className="w-10 h-10 bg-sky-100 rounded-xl flex items-center justify-center">
            {(() => {
              const Icon = allTabs.find(t => t.key === tabKey)?.icon || FileText;
              return <Icon className="w-5 h-5 text-sky-600" />;
            })()}
          </div>
          <div className="text-right">
            <h3 className="text-lg font-black text-slate-800">{content.title}</h3>
            <p className="text-xs text-slate-400 font-bold">محتوى تجريبي - المحاكي</p>
          </div>
          <div className="mr-auto flex items-center gap-1 bg-amber-50 border border-amber-200 text-amber-700 text-[10px] font-bold px-3 py-1 rounded-full">
            <Sparkles className="w-3 h-3" /> معاينة
          </div>
        </div>

        {/* Content */}
        <div className="p-6">
          <p className="text-sm text-slate-600 leading-7 mb-6 text-right">{content.description}</p>
          <div className="space-y-2">
            {content.items.map((item, i) => (
              <div 
                key={i} 
                className="flex items-center gap-3 px-4 py-3 bg-slate-50 rounded-xl hover:bg-sky-50 transition-colors text-right"
              >
                <ArrowRight className="w-3.5 h-3.5 text-slate-300 flex-shrink-0" />
                <span className="text-sm font-bold text-slate-600">{item}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

export default function PatientMockupPage() {
  const [activeTab, setActiveTab] = useState('overview');
  const [showDesignNote, setShowDesignNote] = useState(true);

  return (
    <div className="h-full flex flex-col bg-slate-50 relative">
      {/* Design Note Banner - dismissible */}
      {showDesignNote && (
        <div className="bg-gradient-to-l from-indigo-600 via-blue-600 to-sky-500 text-white px-4 py-2 flex items-center justify-between z-50">
          <div className="flex items-center gap-2">
            <Sparkles className="w-4 h-4 animate-pulse" />
            <span className="text-xs font-bold">محاكي تفاعلي لتصميم ملف المريض بطراز Microsoft Fluent - اضغط على الأزرار والتبويبات لتجربة التصميم</span>
          </div>
          <button 
            onClick={() => setShowDesignNote(false)}
            className="text-white/70 hover:text-white text-xs font-bold px-2 py-0.5 rounded hover:bg-white/10 transition-colors"
          >
            ✕
          </button>
        </div>
      )}

      {/* Patient Banner */}
      <PatientBanner patient={mockPatient} />

      {/* Ribbon Command Bar */}
      <RibbonCommandBar />

      {/* Pivot Tabs */}
      <PivotTabs 
        tabs={allTabs} 
        activeTab={activeTab} 
        onTabChange={setActiveTab} 
      />

      {/* Main Content Area */}
      <div className="flex-1 overflow-auto">
        {activeTab === 'overview' ? (
          <div className="p-6 flex gap-6 animate-fadeIn">
            <TreatmentPlanCard />
            <NextAppointmentCard />
          </div>
        ) : (
          <TabContent tabKey={activeTab} />
        )}
      </div>
    </div>
  );
}
