import Link from "next/link";
import { Stethoscope, Star, Shield, Clock, Phone, MapPin, ChevronLeft, CheckCircle2 } from "lucide-react";

const SERVICES = [
  {
    icon: "🦷",
    title: "تقويم الأسنان",
    desc: "تقويم معدني وشفاف وInvisalign — بأيدي متخصصة وبنتائج مثالية للأطفال والبالغين.",
  },
  {
    icon: "✨",
    title: "طب الأسنان العام",
    desc: "حشو، تنظيف، علاج جذور، تاج وجسور — رعاية شاملة لأسنان صحية طوال العمر.",
  },
  {
    icon: "🔬",
    title: "جراحة الوجه والفكين",
    desc: "خلع الأضراس والضروس العقلية، جراحة تصحيح الفك، وزراعة الأسنان بدقة عالية.",
  },
  {
    icon: "🧹",
    title: "تنظيف الأسنان الدوري",
    desc: "إزالة التهاب اللثة والجير وتلميع الأسنان — للحفاظ على صحة فمك ولثتك.",
  },
  {
    icon: "💎",
    title: "تجميل الأسنان",
    desc: "تبييض الأسنان، قشور البورسلان، ترميم الأسنان — ابتسامة تليق بك.",
  },
  {
    icon: "📸",
    title: "الأشعة والتشخيص",
    desc: "أشعة بانورامية وسيفالومترية رقمية لتشخيص دقيق وخطة علاج فعّالة.",
  },
];

const TEAM = [
  {
    name: "د. عقلان الكامل",
    specialty: "أخصائي تقويم الأسنان",
    desc: "متخصص في تقويم الأسنان لدى الأطفال والبالغين بأحدث التقنيات.",
    color: "#3d7ab5",
    initials: "عك",
  },
  {
    name: "د. عائشة غازي",
    specialty: "طب أسنان عام",
    desc: "خبرة واسعة في طب الأسنان العام والتجميل والعلاجات الشاملة.",
    color: "#f5922e",
    initials: "عغ",
  },
  {
    name: "د. إيمان الكامل",
    specialty: "طب أسنان عام",
    desc: "رعاية لطيفة ومتخصصة لجميع أفراد العائلة من الأطفال للكبار.",
    color: "#059669",
    initials: "إك",
  },
  {
    name: "د. هشام القدسي",
    specialty: "طب أسنان عام",
    desc: "متخصص في علاج جذور الأسنان وترميمها بدقة ومهارة عالية.",
    color: "#7C3AED",
    initials: "هق",
  },
  {
    name: "د. خلدون البريهي",
    specialty: "أخصائي جراحة وجه وفكين",
    desc: "خبير في جراحة الفك والوجه وزراعة الأسنان وخلع الضروس.",
    color: "#DC2626",
    initials: "خب",
  },
];

const WHY_US = [
  { icon: <Star className="w-6 h-6 text-clinic-orange" />, title: "أطباء متخصصون", desc: "فريق من أفضل أطباء الأسنان في تعز بخبرة سنوات طويلة." },
  { icon: <Shield className="w-6 h-6 text-clinic-blue" />, title: "تعقيم وأمان", desc: "معايير تعقيم عالمية لضمان سلامتك وسلامة عائلتك." },
  { icon: <Clock className="w-6 h-6 text-clinic-teal" />, title: "مواعيد مرنة", desc: "نعمل 6 أيام في الأسبوع لخدمتك في أنسب الأوقات." },
  { icon: <CheckCircle2 className="w-6 h-6 text-green-600" />, title: "أسعار مناسبة", desc: "خطط علاج واضحة وأسعار شفافة تناسب جميع المستويات." },
];

export default function PublicHomePage() {
  return (
    <div dir="rtl">
      {/* Hero */}
      <section className="relative bg-gradient-to-bl from-clinic-navy via-clinic-navy-700 to-clinic-blue-500 text-white overflow-hidden">
        <div className="absolute inset-0 opacity-10" style={{ backgroundImage: "url(\"data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%23ffffff' fill-opacity='0.4'%3E%3Cpath d='M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E\")" }} />
        <div className="relative max-w-6xl mx-auto px-4 py-20 md:py-32 flex flex-col md:flex-row items-center gap-12">
          <div className="flex-1 text-center md:text-right">
            <div className="inline-flex items-center gap-2 bg-white/10 rounded-full px-4 py-1 text-sm mb-6">
              <span className="w-2 h-2 rounded-full bg-green-400 animate-pulse" />
              نقبل حجوزات جديدة الآن
            </div>
            <h1 className="text-4xl md:text-5xl font-extrabold leading-snug mb-4">
              ابتسامتك تستحق<br />
              <span className="text-clinic-orange">أفضل رعاية</span>
            </h1>
            <p className="text-blue-100 text-lg mb-8 max-w-lg">
              مركز د. عقلان الكامل لطب وتقويم الأسنان في تعز — خبرة طبية متميزة وتقنيات حديثة لصحة أسنانك.
            </p>
            <div className="flex flex-col sm:flex-row gap-3 justify-center md:justify-start">
              <Link
                href="/home/book"
                className="bg-clinic-orange hover:bg-orange-500 text-white font-bold px-8 py-3 rounded-xl text-lg transition-colors shadow-lg inline-flex items-center gap-2"
              >
                احجز موعدك الآن
                <ChevronLeft className="w-5 h-5" />
              </Link>
              <a
                href="tel:04253028"
                className="border-2 border-white/30 hover:border-white text-white font-semibold px-8 py-3 rounded-xl text-lg transition-colors inline-flex items-center gap-2"
              >
                <Phone className="w-5 h-5" />
                04-253028
              </a>
            </div>
          </div>

          {/* Stats cards */}
          <div className="flex-shrink-0 grid grid-cols-2 gap-4 w-full md:w-auto">
            {[
              { number: "+5", label: "أطباء متخصصون" },
              { number: "+8", label: "سنوات خبرة" },
              { number: "+1000", label: "مريض سعيد" },
              { number: "6", label: "أيام عمل أسبوعياً" },
            ].map((stat) => (
              <div key={stat.label} className="bg-white/10 backdrop-blur rounded-2xl p-5 text-center">
                <div className="text-3xl font-extrabold text-clinic-orange">{stat.number}</div>
                <div className="text-sm text-blue-100 mt-1">{stat.label}</div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Services */}
      <section id="services" className="py-20 bg-gray-50">
        <div className="max-w-6xl mx-auto px-4">
          <div className="text-center mb-12">
            <h2 className="text-3xl font-extrabold text-clinic-navy mb-3">خدماتنا الطبية</h2>
            <p className="text-gray-500 max-w-xl mx-auto">نقدم طيفاً واسعاً من خدمات طب الأسنان تحت سقف واحد بأعلى معايير الجودة</p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {SERVICES.map((service) => (
              <div
                key={service.title}
                className="bg-white rounded-2xl p-6 shadow-card hover:shadow-card-hover transition-shadow border border-gray-100 group"
              >
                <div className="text-4xl mb-4">{service.icon}</div>
                <h3 className="text-lg font-bold text-clinic-navy mb-2 group-hover:text-clinic-blue transition-colors">
                  {service.title}
                </h3>
                <p className="text-gray-500 text-sm leading-relaxed">{service.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Why Us */}
      <section className="py-20 bg-white">
        <div className="max-w-6xl mx-auto px-4">
          <div className="text-center mb-12">
            <h2 className="text-3xl font-extrabold text-clinic-navy mb-3">لماذا تختارنا؟</h2>
            <p className="text-gray-500">نلتزم بتقديم تجربة علاجية متميزة في كل زيارة</p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {WHY_US.map((item) => (
              <div key={item.title} className="text-center p-6 rounded-2xl bg-gray-50 hover:bg-clinic-blue-50 transition-colors">
                <div className="flex justify-center mb-4">{item.icon}</div>
                <h3 className="font-bold text-clinic-navy mb-2">{item.title}</h3>
                <p className="text-gray-500 text-sm leading-relaxed">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Team */}
      <section id="team" className="py-20 bg-gradient-to-b from-gray-50 to-white">
        <div className="max-w-6xl mx-auto px-4">
          <div className="text-center mb-12">
            <h2 className="text-3xl font-extrabold text-clinic-navy mb-3">فريق الأطباء</h2>
            <p className="text-gray-500">نخبة من الأطباء المتخصصين في مختلف تخصصات طب الأسنان</p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {TEAM.map((doctor) => (
              <div key={doctor.name} className="bg-white rounded-2xl p-6 shadow-card hover:shadow-card-hover transition-shadow border border-gray-100 flex items-start gap-4">
                <div
                  className="w-14 h-14 rounded-full flex items-center justify-center text-white font-bold text-lg flex-shrink-0"
                  style={{ backgroundColor: doctor.color }}
                >
                  {doctor.initials}
                </div>
                <div>
                  <div className="font-bold text-clinic-navy">{doctor.name}</div>
                  <div className="text-sm font-medium mb-2" style={{ color: doctor.color }}>{doctor.specialty}</div>
                  <div className="text-sm text-gray-500 leading-relaxed">{doctor.desc}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Gallery Placeholder */}
      <section className="py-20 bg-clinic-navy-700">
        <div className="max-w-6xl mx-auto px-4 text-center">
          <h2 className="text-3xl font-extrabold text-white mb-3">معرض الصور</h2>
          <p className="text-blue-200 mb-8">نماذج من أعمالنا قريباً</p>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <div
                key={i}
                className="aspect-square rounded-2xl bg-clinic-navy flex items-center justify-center text-blue-300 text-sm border border-blue-700"
              >
                🦷
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="py-20 bg-white">
        <div className="max-w-2xl mx-auto px-4 text-center">
          <Stethoscope className="w-12 h-12 text-clinic-blue mx-auto mb-4" />
          <h2 className="text-3xl font-extrabold text-clinic-navy mb-3">هل أنت مستعد للبدء؟</h2>
          <p className="text-gray-500 mb-8">
            احجز استشارتك الأولى اليوم وابدأ رحلتك نحو ابتسامة أجمل وأسنان أصح
          </p>
          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <Link
              href="/home/book"
              className="bg-clinic-blue hover:bg-blue-600 text-white font-bold px-8 py-3 rounded-xl text-lg transition-colors shadow-lg inline-flex items-center gap-2 justify-center"
            >
              احجز الآن مجاناً
              <ChevronLeft className="w-5 h-5" />
            </Link>
            <div className="flex items-center gap-2 text-gray-500 text-sm justify-center">
              <MapPin className="w-4 h-4 text-clinic-orange" />
              تعز، اليمن — شارع التحرير الأعلى
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
