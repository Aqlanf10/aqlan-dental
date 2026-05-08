import Link from "next/link";
import {
  Stethoscope,
  Star,
  Shield,
  Clock,
  CheckCircle2,
  MapPin,
  ChevronLeft,
  GitBranch,
  Layers,
  Sparkles,
  Zap,
  Heart,
  Scissors,
  Activity,
  MessageCircle,
  Users,
  Monitor,
  ClipboardList,
  Plus,
} from "lucide-react";

const SERVICES = [
  {
    icon: GitBranch,
    color: "#3d7ab5",
    title: "تقويم الأسنان",
    desc: "تقويم معدني وشفاف وInvisalign بأيدي متخصصة ونتائج مثالية للأطفال والبالغين.",
  },
  {
    icon: Plus,
    color: "#0E7490",
    title: "زراعة الأسنان",
    desc: "زراعة أسنان فورية ودائمة بتقنيات حديثة وضمان طويل الأمد.",
  },
  {
    icon: Sparkles,
    color: "#f5922e",
    title: "تجميل الأسنان",
    desc: "تبييض، قشور البورسلان، وترميم الأسنان لابتسامة تليق بك.",
  },
  {
    icon: Zap,
    color: "#DC2626",
    title: "علاج العصب",
    desc: "علاج جذور الأسنان بدقة عالية وتخدير فعّال لراحة تامة.",
  },
  {
    icon: Layers,
    color: "#7C3AED",
    title: "تركيبات الأسنان",
    desc: "تاج، جسور، وأطقم أسنان بجودة عالية ومطابقة تامة للفم.",
  },
  {
    icon: Heart,
    color: "#059669",
    title: "طب أسنان الأطفال",
    desc: "رعاية لطيفة ومتخصصة لأسنان أطفالك في بيئة آمنة وودودة.",
  },
  {
    icon: Scissors,
    color: "#D97706",
    title: "جراحة الفم والأسنان",
    desc: "خلع الضروس العقلية وجراحة الفك بيد متخصص جراح معتمد.",
  },
  {
    icon: Stethoscope,
    color: "#3d7ab5",
    title: "الكشف والاستشارات",
    desc: "فحص شامل، خطة علاج واضحة، وأسعار شفافة من أول زيارة.",
  },
];

const WHY_US = [
  {
    icon: Star,
    iconColor: "#f5922e",
    title: "أطباء متخصصون",
    desc: "فريق من أفضل أطباء الأسنان في تعز بخبرة تتجاوز 8 سنوات.",
  },
  {
    icon: Activity,
    iconColor: "#3d7ab5",
    title: "تقنيات حديثة",
    desc: "أجهزة طبية من أحدث الموديلات لتشخيص دقيق وعلاج فعّال.",
  },
  {
    icon: ClipboardList,
    iconColor: "#0E7490",
    title: "خطط علاج واضحة",
    desc: "نشرح لك كل خطوة في العلاج مع وضوح تام في التكاليف.",
  },
  {
    icon: CheckCircle2,
    iconColor: "#059669",
    title: "متابعة بعد العلاج",
    desc: "متابعة دقيقة بعد كل إجراء لضمان أفضل النتائج.",
  },
  {
    icon: Clock,
    iconColor: "#3d7ab5",
    title: "مواعيد منظمة",
    desc: "نحترم وقتك — مواعيد دقيقة وانتظار لا يتجاوز 10 دقائق.",
  },
  {
    icon: Users,
    iconColor: "#f5922e",
    title: "رعاية متكاملة للأسرة",
    desc: "خدمات طب الأسنان لجميع أفراد العائلة من الأطفال للكبار.",
  },
];

const TEAM = [
  {
    name: "د. عقلان الكامل",
    specialty: "أخصائي تقويم الأسنان",
    desc: "متخصص في تقويم الأسنان للأطفال والبالغين بأحدث تقنيات التقويم الثابت والشفاف.",
    color: "#3d7ab5",
    initials: "عك",
  },
  {
    name: "د. عائشة غازي",
    specialty: "طب الأسنان العام والتجميلي",
    desc: "خبرة واسعة في طب الأسنان العام، التجميل، والعلاجات الترميمية الشاملة.",
    color: "#f5922e",
    initials: "عغ",
  },
  {
    name: "د. إيمان الكامل",
    specialty: "طب الأسنان العام",
    desc: "رعاية لطيفة ومتخصصة لجميع أفراد العائلة من الأطفال حتى كبار السن.",
    color: "#059669",
    initials: "إك",
  },
  {
    name: "د. هشام القدسي",
    specialty: "طب الأسنان العام",
    desc: "متخصص في معالجة جذور الأسنان وترميمها وعلاج أمراض اللثة بدقة.",
    color: "#7C3AED",
    initials: "هق",
  },
  {
    name: "د. خلدون البريهي",
    specialty: "أخصائي جراحة الفم والوجه والفكين",
    desc: "خبير في جراحة الفك والوجه، زراعة الأسنان، وخلع الضروس المعقدة.",
    color: "#DC2626",
    initials: "خب",
  },
];

const GALLERY = [
  {
    gradientFrom: "from-blue-600",
    gradientTo: "to-blue-800",
    icon: Users,
    title: "استقبال متميز",
    subtitle: "بيئة ترحيبية ومريحة",
  },
  {
    gradientFrom: "from-teal-600",
    gradientTo: "to-teal-800",
    icon: Activity,
    title: "وحدات علاج حديثة",
    subtitle: "تجهيزات طبية متطورة",
  },
  {
    gradientFrom: "from-emerald-600",
    gradientTo: "to-emerald-800",
    icon: Shield,
    title: "قسم التعقيم الكامل",
    subtitle: "معايير تعقيم عالمية",
  },
  {
    gradientFrom: "from-purple-600",
    gradientTo: "to-purple-800",
    icon: Monitor,
    title: "أجهزة تصوير رقمية",
    subtitle: "تشخيص دقيق وفوري",
  },
];

export default function PublicHomePage() {
  return (
    <div dir="rtl">
      {/* ======== SECTION 1 — Hero ======== */}
      <section className="relative bg-gradient-to-bl from-clinic-navy via-clinic-navy-700 to-clinic-blue-500 text-white overflow-hidden py-16 md:py-24">
        {/* subtle dot pattern overlay */}
        <div
          className="absolute inset-0 opacity-10"
          style={{
            backgroundImage:
              "url(\"data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%23ffffff' fill-opacity='0.4'%3E%3Cpath d='M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E\")",
          }}
        />

        <div className="relative max-w-6xl mx-auto px-4">
          {/* Main hero content */}
          <div className="text-center md:text-right max-w-3xl md:mx-0 mx-auto">
            {/* Availability badge */}
            <div className="inline-flex items-center gap-2 bg-white/10 backdrop-blur-sm border border-white/20 rounded-full px-4 py-1.5 text-sm mb-8">
              <span className="w-2 h-2 rounded-full bg-green-400 animate-pulse flex-shrink-0" />
              نقبل حجوزات جديدة
            </div>

            {/* Headline */}
            <h1 className="text-4xl md:text-6xl font-extrabold leading-snug mb-5">
              ابتسامتك تبدأ
              <br />
              من{" "}
              <span className="text-clinic-orange">مركز متخصص</span>
            </h1>

            <p className="text-blue-100 text-lg md:text-xl mb-8 leading-relaxed max-w-2xl md:mx-0 mx-auto">
              مركز د. عقلان الكامل — خبرة طبية متميزة وتقنيات حديثة لأسنان صحية وابتسامة جميلة
            </p>

            {/* Trust badges */}
            <div className="flex flex-wrap gap-2 justify-center md:justify-start mb-10">
              {["تقويم الأسنان", "زراعة الأسنان", "تجميل الأسنان", "علاج العصب"].map((badge) => (
                <span
                  key={badge}
                  className="inline-flex items-center gap-1.5 bg-white/10 border border-white/20 rounded-full px-3 py-1 text-xs font-medium"
                >
                  <CheckCircle2 className="w-3.5 h-3.5 text-green-400" />
                  {badge}
                </span>
              ))}
            </div>

            {/* CTA buttons */}
            <div className="flex flex-col sm:flex-row gap-3 justify-center md:justify-start">
              <Link
                href="/home/book"
                className="bg-clinic-orange hover:bg-orange-500 text-white font-bold px-8 py-3.5 rounded-xl text-lg transition-colors shadow-lg inline-flex items-center justify-center gap-2"
              >
                احجز موعدك الآن
                <ChevronLeft className="w-5 h-5" />
              </Link>
              <a
                href="https://wa.me/967770245745"
                target="_blank"
                rel="noopener noreferrer"
                className="border-2 border-white/30 hover:border-white hover:bg-white/10 text-white font-semibold px-6 py-3.5 rounded-xl text-base transition-colors inline-flex items-center justify-center gap-2"
              >
                <MessageCircle className="w-5 h-5 text-green-400" />
                واتساب
              </a>
              <a
                href="tel:04253028"
                className="border-2 border-white/20 hover:border-white/50 text-white/80 hover:text-white font-medium px-6 py-3.5 rounded-xl text-base transition-colors inline-flex items-center justify-center gap-2"
              >
                <MapPin className="w-4 h-4 opacity-70" />
                04-253028
              </a>
            </div>

            {/* Soft secondary links */}
            <div className="mt-4 flex items-center gap-4 justify-center md:justify-start">
              <Link
                href="/portal/login"
                className="text-blue-200/70 hover:text-blue-100 text-sm transition-colors inline-flex items-center gap-1.5"
              >
                بوابة المرضى
              </Link>
            </div>
          </div>

          {/* Stats grid */}
          <div className="mt-14 grid grid-cols-2 md:grid-cols-4 gap-4">
            {[
              { number: "+5", label: "أطباء متخصصون" },
              { number: "+8", label: "سنوات خبرة" },
              { number: "+3000", label: "مريض سعيد" },
              { number: "6", label: "أيام عمل أسبوعياً" },
            ].map((stat) => (
              <div
                key={stat.label}
                className="bg-white/10 backdrop-blur-sm border border-white/15 rounded-2xl p-5 text-center"
              >
                <div className="text-3xl font-extrabold text-clinic-orange mb-1">{stat.number}</div>
                <div className="text-sm text-blue-200">{stat.label}</div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ======== SECTION 2 — Services ======== */}
      <section id="services" className="py-20 bg-gray-50">
        <div className="max-w-6xl mx-auto px-4">
          <div className="text-center mb-12">
            <span className="inline-block bg-clinic-blue-100 text-clinic-blue px-4 py-1 rounded-full text-xs font-semibold mb-3 uppercase tracking-wide">
              خدماتنا
            </span>
            <h2 className="text-3xl md:text-4xl font-extrabold text-clinic-navy mb-3">خدماتنا الطبية</h2>
            <p className="text-gray-500 max-w-xl mx-auto leading-relaxed">
              نقدم طيفاً واسعاً من خدمات طب الأسنان تحت سقف واحد بأعلى معايير الجودة
            </p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {SERVICES.map((service) => {
              const Icon = service.icon;
              return (
                <div
                  key={service.title}
                  className="bg-white rounded-2xl p-6 shadow-card hover:shadow-card-hover transition-all duration-200 border border-gray-100 group cursor-default"
                >
                  <div
                    className="w-12 h-12 rounded-xl flex items-center justify-center mb-4 transition-transform group-hover:scale-110"
                    style={{ backgroundColor: `${service.color}1a` }}
                  >
                    <Icon className="w-6 h-6" style={{ color: service.color }} />
                  </div>
                  <h3 className="text-base font-bold text-clinic-navy mb-2 group-hover:text-clinic-blue transition-colors">
                    {service.title}
                  </h3>
                  <p className="text-gray-500 text-sm leading-relaxed">{service.desc}</p>
                </div>
              );
            })}
          </div>
        </div>
      </section>

      {/* ======== SECTION 3 — Why Choose Us ======== */}
      <section className="py-20 bg-white">
        <div className="max-w-6xl mx-auto px-4">
          <div className="text-center mb-12">
            <span className="inline-block bg-clinic-orange-100 text-clinic-orange px-4 py-1 rounded-full text-xs font-semibold mb-3 uppercase tracking-wide">
              لماذا نحن
            </span>
            <h2 className="text-3xl md:text-4xl font-extrabold text-clinic-navy mb-3">لماذا تختارنا؟</h2>
            <p className="text-gray-500 leading-relaxed">نلتزم بتقديم تجربة علاجية متميزة في كل زيارة</p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {WHY_US.map((item) => {
              const Icon = item.icon;
              return (
                <div
                  key={item.title}
                  className="text-center p-6 rounded-2xl bg-gray-50 hover:bg-clinic-blue-50 transition-colors border border-transparent hover:border-clinic-blue-100 cursor-default"
                >
                  <div
                    className="w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4"
                    style={{ backgroundColor: `${item.iconColor}1a` }}
                  >
                    <Icon className="w-7 h-7" style={{ color: item.iconColor }} />
                  </div>
                  <h3 className="font-bold text-clinic-navy mb-2 text-base">{item.title}</h3>
                  <p className="text-gray-500 text-sm leading-relaxed">{item.desc}</p>
                </div>
              );
            })}
          </div>
        </div>
      </section>

      {/* ======== SECTION 4 — Team ======== */}
      <section id="team" className="py-20 bg-gradient-to-b from-gray-50 to-white">
        <div className="max-w-6xl mx-auto px-4">
          <div className="text-center mb-12">
            <span className="inline-block bg-clinic-blue-100 text-clinic-blue px-4 py-1 rounded-full text-xs font-semibold mb-3 uppercase tracking-wide">
              الفريق الطبي
            </span>
            <h2 className="text-3xl md:text-4xl font-extrabold text-clinic-navy mb-3">فريق الأطباء</h2>
            <p className="text-gray-500 leading-relaxed">نخبة من الأطباء المتخصصين في مختلف تخصصات طب الأسنان</p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {TEAM.map((doctor) => (
              <div
                key={doctor.name}
                className="bg-white rounded-2xl shadow-card hover:shadow-card-hover transition-all duration-200 border border-gray-100 overflow-hidden"
                style={{ borderTopColor: doctor.color, borderTopWidth: "3px" }}
              >
                <div className="p-6 flex items-start gap-4">
                  <div
                    className="w-16 h-16 rounded-full flex items-center justify-center text-white font-bold text-xl flex-shrink-0 shadow-md"
                    style={{ backgroundColor: doctor.color }}
                  >
                    {doctor.initials}
                  </div>
                  <div className="min-w-0">
                    <div className="font-bold text-clinic-navy text-base leading-snug">{doctor.name}</div>
                    <div className="text-sm font-semibold mb-2 mt-0.5" style={{ color: doctor.color }}>
                      {doctor.specialty}
                    </div>
                    <div className="text-sm text-gray-500 leading-relaxed">{doctor.desc}</div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ======== SECTION 5 — Facilities Gallery ======== */}
      <section className="py-20 bg-clinic-navy">
        <div className="max-w-6xl mx-auto px-4">
          <div className="text-center mb-12">
            <span className="inline-block bg-white/10 text-blue-200 border border-white/20 px-4 py-1 rounded-full text-xs font-semibold mb-3 uppercase tracking-wide">
              مرافقنا
            </span>
            <h2 className="text-3xl md:text-4xl font-extrabold text-white mb-3">مرافقنا الطبية</h2>
            <p className="text-blue-200 leading-relaxed">بيئة علاجية متكاملة ومجهزة بأحدث التقنيات</p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-5">
            {GALLERY.map((card) => {
              const Icon = card.icon;
              return (
                <div
                  key={card.title}
                  className={`aspect-[4/3] rounded-2xl overflow-hidden relative flex flex-col items-center justify-center text-white p-6 text-center bg-gradient-to-br ${card.gradientFrom} ${card.gradientTo} shadow-lg`}
                >
                  <Icon className="w-12 h-12 mb-3 opacity-90" />
                  <div className="text-lg font-bold">{card.title}</div>
                  <div className="text-sm opacity-75 mt-1">{card.subtitle}</div>
                </div>
              );
            })}
          </div>
        </div>
      </section>

      {/* ======== SECTION 6 — CTA ======== */}
      <section className="py-20 bg-gradient-to-br from-clinic-navy-700 to-clinic-navy text-white">
        <div className="max-w-2xl mx-auto px-4 text-center">
          <div className="w-16 h-16 rounded-full bg-clinic-orange/20 flex items-center justify-center mx-auto mb-6 ring-4 ring-clinic-orange/10">
            <Stethoscope className="w-8 h-8 text-clinic-orange" />
          </div>
          <h2 className="text-3xl md:text-4xl font-extrabold mb-4">
            جاهز تبدأ رحلتك نحو ابتسامة أجمل؟
          </h2>
          <p className="text-blue-200 text-lg mb-4 leading-relaxed">
            احجز استشارتك الأولى اليوم — فريقنا جاهز لمساعدتك
          </p>
          <p className="text-blue-300 text-sm mb-10 flex flex-col sm:flex-row items-center justify-center gap-3">
            <span className="inline-flex items-center gap-1.5">
              <MapPin className="w-4 h-4 text-clinic-orange" />
              تعز، اليمن — شارع التحرير الأعلى
            </span>
            <span className="hidden sm:block text-blue-600">|</span>
            <span className="inline-flex items-center gap-1.5">
              <Clock className="w-4 h-4 text-clinic-orange" />
              السبت – الخميس: 8 ص – 8 م
            </span>
          </p>
          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <Link
              href="/home/book"
              className="bg-clinic-orange hover:bg-orange-500 text-white font-bold px-8 py-3.5 rounded-xl text-lg transition-colors shadow-lg inline-flex items-center justify-center gap-2"
            >
              احجز موعدك الآن
              <ChevronLeft className="w-5 h-5" />
            </Link>
            <a
              href="https://wa.me/967770245745"
              target="_blank"
              rel="noopener noreferrer"
              className="border-2 border-white/30 hover:border-white hover:bg-white/10 text-white font-semibold px-6 py-3.5 rounded-xl text-base transition-colors inline-flex items-center justify-center gap-2"
            >
              <MessageCircle className="w-5 h-5 text-green-400" />
              تواصل عبر واتساب
            </a>
          </div>
        </div>
      </section>
    </div>
  );
}
