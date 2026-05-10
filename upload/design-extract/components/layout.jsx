
// ============================
// Aqlan Dental Pro — Layout
// ============================

const { useState, useRef, useEffect } = React;

// ── Icon component ──────────────────────────────────────────
const PATHS = {
  home: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6',
  users: 'M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z',
  calendar: 'M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z',
  tooth: 'M12 2C8 2 5 5 5 9c0 2.5 1 4.5 2.5 6L9 21h6l1.5-6C18 13.5 19 11.5 19 9c0-4-3-7-7-7z',
  chart: 'M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z',
  money: 'M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z',
  settings: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z',
  bell: 'M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9',
  search: 'M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z',
  plus: 'M12 4v16m8-8H4',
  chevronDown: 'M19 9l-7 7-7-7',
  chevronLeft: 'M15 19l-7-7 7-7',
  menu: 'M4 6h16M4 12h16M4 18h16',
  x: 'M6 18L18 6M6 6l12 12',
  check: 'M5 13l4 4L19 7',
  clock: 'M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z',
  user: 'M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z',
  clipboard: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2',
  flask: 'M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z',
  box: 'M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 10V7m-8 3l8 4',
  report: 'M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z',
  referral: 'M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4',
  scalpel: 'M14.5 10l-4.5 4.5M10 6l4 4m-4-4L6 10m4-4V3m0 3l4 4M6 18l12-12',
  logout: 'M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1',
  phone: 'M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z',
  message: 'M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z',
  eye: 'M15 12a3 3 0 11-6 0 3 3 0 016 0z M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z',
  edit: 'M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z',
  trash: 'M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16',
  ai: 'M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z',
  arrow_right: 'M13 7l5 5m0 0l-5 5m5-5H6',
};

function Icon({ name, size = 18, stroke = 'currentColor', strokeWidth = 1.8 }) {
  const d = PATHS[name] || PATHS.x;
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={stroke} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round">
      {d.split(' M').map((part, i) => (
        <path key={i} d={i === 0 ? part : 'M' + part} />
      ))}
    </svg>
  );
}

// ── Badge ──────────────────────────────────────────
function Badge({ children, color = '#3d7ab5', bg }) {
  const bgColor = bg || color + '18';
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 4,
      padding: '2px 10px', borderRadius: 99,
      fontSize: 12, fontWeight: 600,
      color, background: bgColor,
    }}>
      {children}
    </span>
  );
}

// ── Button ──────────────────────────────────────────
function Btn({ children, variant = 'primary', size = 'md', onClick, icon, style: extraStyle = {} }) {
  const [hovered, setHovered] = useState(false);
  const base = {
    display: 'inline-flex', alignItems: 'center', gap: 6,
    borderRadius: 8, fontFamily: 'Tajawal', fontWeight: 600, cursor: 'pointer',
    transition: 'all 0.15s', border: 'none', outline: 'none',
    fontSize: size === 'sm' ? 13 : 14,
    padding: size === 'sm' ? '5px 12px' : '8px 18px',
  };
  const variants = {
    primary: { background: hovered ? '#2d5e8e' : '#3d7ab5', color: '#fff' },
    orange: { background: hovered ? '#e07d1e' : '#f5922e', color: '#fff' },
    outline: { background: hovered ? '#eef3f9' : '#fff', color: '#3d7ab5', border: '1.5px solid #3d7ab5' },
    ghost: { background: hovered ? '#eef3f9' : 'transparent', color: '#3d7ab5' },
    danger: { background: hovered ? '#dc2626' : '#ef4444', color: '#fff' },
  };
  return (
    <button
      style={{ ...base, ...variants[variant], ...extraStyle }}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      onClick={onClick}
    >
      {icon && <Icon name={icon} size={size === 'sm' ? 14 : 16} />}
      {children}
    </button>
  );
}

// ── Card ──────────────────────────────────────────
function Card({ children, style = {}, padding = 20 }) {
  return (
    <div style={{
      background: '#fff',
      borderRadius: 12,
      padding,
      boxShadow: '0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)',
      border: '1px solid #e8f0f9',
      ...style,
    }}>
      {children}
    </div>
  );
}

// ── StatCard ──────────────────────────────────────────
function StatCard({ label, value, icon, color, sub }) {
  return (
    <Card style={{ flex: 1 }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between' }}>
        <div>
          <div style={{ fontSize: 13, color: '#64748b', fontWeight: 500, marginBottom: 6 }}>{label}</div>
          <div style={{ fontSize: 26, fontWeight: 800, color: '#0d2137', letterSpacing: -0.5 }}>{value}</div>
          {sub && <div style={{ fontSize: 12, color: '#94a3b8', marginTop: 4 }}>{sub}</div>}
        </div>
        <div style={{ width: 44, height: 44, borderRadius: 12, background: color + '18', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Icon name={icon} size={22} stroke={color} />
        </div>
      </div>
    </Card>
  );
}

// ── Nav items ──────────────────────────────────────────
const NAV = [
  { section: 'رئيسي' },
  { id: 'dashboard', label: 'لوحة التحكم', icon: 'home' },
  { id: 'patients', label: 'المرضى', icon: 'users' },
  { id: 'appointments', label: 'المواعيد', icon: 'calendar' },
  { section: 'تخصصات' },
  { id: 'ortho', label: 'التقويم', icon: 'tooth' },
  { id: 'ceph', label: 'السيفالومتري المتقدم', icon: 'chart' },
  { id: 'vto', label: 'VTO — محاكاة العلاج', icon: 'ai' },
  { id: 'general', label: 'مخطط الأسنان', icon: 'clipboard' },
  { id: 'surgery', label: 'جراحة الوجه والفكين', icon: 'scalpel' },
  { section: 'التواصل' },
  { id: 'messaging', label: 'الرسائل', icon: 'bell', badge: 3 },
  { id: 'sms', label: 'تذكيرات SMS', icon: 'phone' },
  { id: 'recall', label: 'نظام الاستدعاء', icon: 'users' },
  { section: 'عمليات' },
  { id: 'finance', label: 'المالية', icon: 'money' },
  { id: 'questionnaires', label: 'الاستبيانات', icon: 'clipboard' },
  { id: 'referrals', label: 'الإحالات', icon: 'referral' },
  { id: 'lab', label: 'طلبات المختبر', icon: 'flask' },
  { id: 'inventory', label: 'المخزون', icon: 'box' },
  { section: 'تقارير' },
  { id: 'reports', label: 'التقارير والإحصائيات', icon: 'report' },
  { section: 'النظام' },
  { id: 'settings', label: 'الإعدادات', icon: 'settings' },
];

// ── Sidebar ──────────────────────────────────────────
function Sidebar({ page, onNavigate, collapsed, onToggle }) {
  const w = collapsed ? 64 : 258;
  return (
    <div style={{
      width: w, minWidth: w, height: '100vh',
      background: '#0d2137',
      display: 'flex', flexDirection: 'column',
      transition: 'width 0.25s ease',
      position: 'sticky', top: 0,
      overflow: 'hidden',
      zIndex: 100,
    }}>
      {/* Logo */}
      <div style={{ padding: collapsed ? '16px 0' : '16px 18px', display: 'flex', alignItems: 'center', gap: 10, borderBottom: '1px solid rgba(255,255,255,0.08)', minHeight: 72 }}>
        <img src="uploads/logo_upload-1777339394562.png" style={{ width: 38, height: 38, borderRadius: 8, objectFit: 'contain', background: '#fff', padding: 2, flexShrink: 0 }} alt="Aqlan" />
        {!collapsed && (
          <div>
            <div style={{ color: '#fff', fontWeight: 800, fontSize: 14, lineHeight: 1.2 }}>Aqlan Dental Pro</div>
            <div style={{ color: 'rgba(255,255,255,0.45)', fontSize: 11, marginTop: 1 }}>مركز د. عقلان الكامل</div>
          </div>
        )}
      </div>

      {/* Nav */}
      <div style={{ flex: 1, overflowY: 'auto', overflowX: 'hidden', padding: '8px 0' }}>
        {NAV.map((item, i) => {
          if (item.section) {
            if (collapsed) return null;
            return (
              <div key={i} style={{ padding: '14px 18px 4px', fontSize: 10, fontWeight: 700, color: 'rgba(255,255,255,0.3)', textTransform: 'uppercase', letterSpacing: 1 }}>
                {item.section}
              </div>
            );
          }
          const active = page === item.id;
          return (
            <button key={item.id}
              onClick={() => onNavigate(item.id)}
              title={collapsed ? item.label : ''}
              style={{
                display: 'flex', alignItems: 'center', gap: 10,
                width: '100%', padding: collapsed ? '10px 0' : '10px 18px',
                justifyContent: collapsed ? 'center' : 'flex-start',
                background: active ? 'rgba(61,122,181,0.35)' : 'transparent',
                borderRight: active ? '3px solid #3d7ab5' : '3px solid transparent',
                color: active ? '#fff' : 'rgba(255,255,255,0.6)',
                border: 'none', cursor: 'pointer', fontFamily: 'Tajawal',
                fontSize: 14, fontWeight: active ? 700 : 500,
                transition: 'all 0.15s',
                textAlign: 'right',
              }}
              onMouseEnter={e => { if (!active) e.currentTarget.style.background = 'rgba(255,255,255,0.05)'; }}
              onMouseLeave={e => { if (!active) e.currentTarget.style.background = 'transparent'; }}
            >
              <Icon name={item.icon} size={18} stroke={active ? '#3d7ab5' : 'rgba(255,255,255,0.6)'} />
              {!collapsed && <span style={{ flex: 1 }}>{item.label}</span>}
              {!collapsed && item.badge > 0 && (
                <span style={{ background: '#ef4444', color: '#fff', borderRadius: 99, padding: '1px 6px', fontSize: 10, fontWeight: 800, marginRight: 2 }}>{item.badge}</span>
              )}
              {collapsed && item.badge > 0 && (
                <span style={{ position: 'absolute', top: 6, right: 6, width: 8, height: 8, borderRadius: 99, background: '#ef4444', border: '1.5px solid #0d2137' }} />
              )}
            </button>
          );
        })}
      </div>

      {/* User */}
      <div style={{ padding: collapsed ? '12px 0' : '12px 16px', borderTop: '1px solid rgba(255,255,255,0.08)', display: 'flex', alignItems: 'center', gap: 10, justifyContent: collapsed ? 'center' : 'flex-start' }}>
        <div style={{ width: 34, height: 34, borderRadius: 99, background: '#3d7ab5', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 13, fontWeight: 700, color: '#fff', flexShrink: 0 }}>
          عك
        </div>
        {!collapsed && (
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ color: '#fff', fontSize: 13, fontWeight: 700, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>د. عقلان الكامل</div>
            <div style={{ color: 'rgba(255,255,255,0.4)', fontSize: 11 }}>مدير النظام</div>
          </div>
        )}
      </div>

      {/* Collapse btn */}
      <button onClick={onToggle} style={{ position: 'absolute', top: 22, left: 0, width: 24, height: 24, background: '#1a3a5c', border: '1px solid rgba(255,255,255,0.15)', borderRadius: '0 6px 6px 0', display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer', color: 'rgba(255,255,255,0.5)', transform: collapsed ? 'none' : 'none' }}>
        <Icon name={collapsed ? 'chevronLeft' : 'menu'} size={12} />
      </button>
    </div>
  );
}

// ── Clock ──────────────────────────────────────────
function LiveClock() {
  const [now, setNow] = useState(new Date());
  useEffect(() => {
    const t = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(t);
  }, []);
  const days = ['الأحد','الإثنين','الثلاثاء','الأربعاء','الخميس','الجمعة','السبت'];
  const months = ['يناير','فبراير','مارس','أبريل','مايو','يونيو','يوليو','أغسطس','سبتمبر','أكتوبر','نوفمبر','ديسمبر'];
  const hh = now.getHours().toString().padStart(2,'0');
  const mm = now.getMinutes().toString().padStart(2,'0');
  const ss = now.getSeconds().toString().padStart(2,'0');
  const dayName = days[now.getDay()];
  const dateStr = `${now.getDate()} ${months[now.getMonth()]} ${now.getFullYear()}`;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', background: '#f0f5fb', borderRadius: 10, padding: '4px 14px', border: '1px solid #dce8f5', minWidth: 140 }}>
      <div style={{ fontSize: 18, fontWeight: 800, color: '#0d2137', letterSpacing: 1, fontFamily: 'monospace', lineHeight: 1.2 }}>
        {hh}<span style={{ opacity: now.getSeconds() % 2 === 0 ? 1 : 0.3, transition: 'opacity 0.3s' }}>:</span>{mm}<span style={{ fontSize: 12, opacity: 0.5 }}>:{ss}</span>
      </div>
      <div style={{ fontSize: 10, color: '#94a3b8', fontWeight: 600 }}>{dayName} · {dateStr}</div>
    </div>
  );
}

// ── Topbar ──────────────────────────────────────────
function Topbar({ title, onNavigate, notifCount = 0, showSearch = true, onLogout }) {
  const [showNotif, setShowNotif] = useState(false);
  const [search, setSearch] = useState('');
  const [showUserMenu, setShowUserMenu] = useState(false);
  return (
    <div style={{ height: 64, background: '#fff', borderBottom: '1px solid #e8f0f9', display: 'flex', alignItems: 'center', padding: '0 24px', gap: 14, position: 'sticky', top: 0, zIndex: 50 }}>
      <div style={{ flex: 1, fontSize: 18, fontWeight: 800, color: '#0d2137' }}>{title}</div>
      <LiveClock />

      {showSearch && (
        <div style={{ position: 'relative' }}>
          <input
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="بحث سريع..."
            style={{ width: 220, padding: '7px 36px 7px 12px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none', background: '#f7fafd', color: '#0d2137', direction: 'rtl' }}
          />
          <span style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: '#94a3b8' }}>
            <Icon name="search" size={15} />
          </span>
        </div>
      )}

      {/* Notifications */}
      <div style={{ position: 'relative' }}>
        <button onClick={() => setShowNotif(!showNotif)} style={{ width: 38, height: 38, borderRadius: 8, background: '#f0f5fb', border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#64748b', position: 'relative' }}>
          <Icon name="bell" size={18} />
          {notifCount > 0 && (
            <span style={{ position: 'absolute', top: 6, right: 6, width: 8, height: 8, borderRadius: 99, background: '#ef4444', border: '2px solid #fff' }} />
          )}
        </button>
        {showNotif && (
          <div style={{ position: 'absolute', top: 44, left: 0, width: 300, background: '#fff', borderRadius: 12, boxShadow: '0 8px 30px rgba(0,0,0,0.12)', border: '1px solid #e8f0f9', zIndex: 200 }}>
            <div style={{ padding: '12px 16px', borderBottom: '1px solid #e8f0f9', fontWeight: 700, fontSize: 14 }}>الإشعارات</div>
            {(MOCK.notifications || []).map(n => (
              <div key={n.id} style={{ padding: '10px 16px', display: 'flex', gap: 10, alignItems: 'flex-start', background: n.read ? '#fff' : '#f0f5fb', borderBottom: '1px solid #f1f5f9' }}>
                <div style={{ width: 8, height: 8, borderRadius: 99, background: n.read ? '#cbd5e1' : '#3d7ab5', marginTop: 5, flexShrink: 0 }} />
                <div>
                  <div style={{ fontSize: 13, color: '#0d2137' }}>{n.text}</div>
                  <div style={{ fontSize: 11, color: '#94a3b8', marginTop: 2 }}>{n.time}</div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* User menu */}
      <div style={{ position: 'relative' }}>
        <div onClick={() => setShowUserMenu(!showUserMenu)} style={{ width: 38, height: 38, borderRadius: 99, background: '#3d7ab5', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 13, fontWeight: 700, color: '#fff', cursor: 'pointer', border: '2px solid #dce8f5' }}>
          عك
        </div>
        {showUserMenu && (
          <div style={{ position: 'absolute', top: 46, left: 0, width: 200, background: '#fff', borderRadius: 12, boxShadow: '0 8px 30px rgba(0,0,0,0.12)', border: '1px solid #e8f0f9', zIndex: 200, overflow: 'hidden' }}>
            <div style={{ padding: '14px 16px', borderBottom: '1px solid #f1f5f9', background: '#f7fafd' }}>
              <div style={{ fontWeight: 700, fontSize: 14, color: '#0d2137' }}>د. عقلان الكامل</div>
              <div style={{ fontSize: 12, color: '#94a3b8', marginTop: 2 }}>مدير النظام</div>
            </div>
            {[
              { icon: 'user', label: 'الملف الشخصي', action: () => setShowUserMenu(false) },
              { icon: 'settings', label: 'الإعدادات', action: () => { onNavigate && onNavigate('settings'); setShowUserMenu(false); } },
            ].map(item => (
              <button key={item.label} onClick={item.action} style={{ width: '100%', padding: '10px 16px', border: 'none', background: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 10, fontFamily: 'Tajawal', fontSize: 13, color: '#0d2137', textAlign: 'right' }}
                onMouseEnter={e => e.currentTarget.style.background = '#f7fafd'}
                onMouseLeave={e => e.currentTarget.style.background = 'none'}
              >
                <Icon name={item.icon} size={15} stroke="#64748b" />
                {item.label}
              </button>
            ))}
            <div style={{ borderTop: '1px solid #f1f5f9' }}>
              <button onClick={() => { setShowUserMenu(false); onLogout && onLogout(); }} style={{ width: '100%', padding: '10px 16px', border: 'none', background: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 10, fontFamily: 'Tajawal', fontSize: 13, color: '#ef4444', textAlign: 'right' }}
                onMouseEnter={e => e.currentTarget.style.background = '#fef2f2'}
                onMouseLeave={e => e.currentTarget.style.background = 'none'}
              >
                <Icon name="logout" size={15} stroke="#ef4444" />
                تسجيل الخروج
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

// ── AppLayout ──────────────────────────────────────────
function AppLayout({ children, page, onNavigate, title, onLogout }) {
  const [collapsed, setCollapsed] = useState(false);
  const unread = (MOCK.notifications || []).filter(n => !n.read).length;
  return (
    <div style={{ display: 'flex', height: '100vh', overflow: 'hidden', direction: 'rtl' }}>
      <Sidebar page={page} onNavigate={onNavigate} collapsed={collapsed} onToggle={() => setCollapsed(!collapsed)} />
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', background: '#eef3f9' }}>
        <Topbar title={title} onNavigate={onNavigate} notifCount={unread} onLogout={onLogout} />
        <div style={{ flex: 1, overflowY: 'auto', padding: 24 }}>
          {children}
        </div>
      </div>
    </div>
  );
}

// ── Section Header ──────────────────────────────────────────
function SectionHeader({ title, action }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
      <h2 style={{ fontSize: 16, fontWeight: 800, color: '#0d2137' }}>{title}</h2>
      {action}
    </div>
  );
}

// ── Tabs ──────────────────────────────────────────
function Tabs({ tabs, active, onChange }) {
  return (
    <div style={{ display: 'flex', gap: 2, borderBottom: '2px solid #e8f0f9', marginBottom: 20, overflowX: 'auto', paddingBottom: 0 }}>
      {tabs.map(t => (
        <button key={t.id} onClick={() => onChange(t.id)} style={{
          padding: '9px 16px', border: 'none', background: 'none', cursor: 'pointer',
          fontFamily: 'Tajawal', fontSize: 13, fontWeight: active === t.id ? 700 : 500,
          color: active === t.id ? '#3d7ab5' : '#64748b',
          borderBottom: active === t.id ? '2px solid #3d7ab5' : '2px solid transparent',
          marginBottom: -2, whiteSpace: 'nowrap', transition: 'all 0.15s',
        }}>
          {t.label}
        </button>
      ))}
    </div>
  );
}

// ── Status Badge helper ──────────────────────────────────────────
function apptStatusLabel(status) {
  const map = {
    completed: { label: 'مكتمل', color: '#22c55e' },
    in_progress: { label: 'جارٍ', color: '#3d7ab5' },
    arrived: { label: 'حضر', color: '#f5922e' },
    scheduled: { label: 'مجدول', color: '#64748b' },
    cancelled: { label: 'ملغي', color: '#ef4444' },
    no_show: { label: 'غياب', color: '#ef4444' },
  };
  return map[status] || map.scheduled;
}

function fmt(n) {
  return n.toLocaleString('ar-YE');
}

Object.assign(window, { Icon, Badge, Btn, Card, StatCard, AppLayout, Sidebar, Topbar, Tabs, SectionHeader, apptStatusLabel, fmt });
