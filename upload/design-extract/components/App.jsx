
// ============================
// App — Main Router
// ============================

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "darkMode": false,
  "sidebarCollapsed": false,
  "accentColor": "#3d7ab5",
  "fontSize": 14
}/*EDITMODE-END*/;

function App() {
  const [loggedIn, setLoggedIn] = React.useState(false);
  const [page, setPage] = React.useState('dashboard');
  const [selectedPatientId, setSelectedPatientId] = React.useState(null);
  const [orthoPatient, setOrthoPatient] = React.useState(null);
  const [showNewPatient, setShowNewPatient] = React.useState(false);
  const [showPrescription, setShowPrescription] = React.useState(false);
  const [darkMode, setDarkMode] = React.useState(TWEAK_DEFAULTS.darkMode);
  const [showTweaks, setShowTweaks] = React.useState(false);

  // Dark mode CSS injection
  React.useEffect(() => {
    if (darkMode) {
      document.body.style.background = '#0d1a2b';
      const style = document.getElementById('dark-mode-style') || document.createElement('style');
      style.id = 'dark-mode-style';
      style.textContent = `
        body { background: #0d1a2b !important; }
        [style*="background: #fff"], [style*="background:#fff"] { background: #132035 !important; }
        [style*="background: #eef3f9"] { background: #0d1a2b !important; }
        [style*="background: #f7fafd"] { background: #0f1e30 !important; }
        [style*="background: #f0f5fb"] { background: #102030 !important; }
        [style*="background: #f1f5f9"] { background: #0f1e30 !important; }
        [style*="color: #0d2137"] { color: #e2eaf4 !important; }
        [style*="color: #64748b"] { color: #94a3b8 !important; }
        [style*="border: 1px solid #e8f0f9"] { border-color: #1e3a5c !important; }
        [style*="border-bottom: 1px solid #f1f5f9"] { border-color: #1e3a5c !important; }
        [style*="border-bottom: 2px solid #e8f0f9"] { border-color: #1e3a5c !important; }
      `;
      document.head.appendChild(style);
    } else {
      const style = document.getElementById('dark-mode-style');
      if (style) style.remove();
      document.body.style.background = '';
    }
  }, [darkMode]);

  // Tweaks panel protocol
  React.useEffect(() => {
    const handler = (e) => {
      if (e.data?.type === '__activate_edit_mode') setShowTweaks(true);
      if (e.data?.type === '__deactivate_edit_mode') setShowTweaks(false);
    };
    window.addEventListener('message', handler);
    window.parent.postMessage({ type: '__edit_mode_available' }, '*');
    return () => window.removeEventListener('message', handler);
  }, []);

  const handleNavigate = (p) => {
    setPage(p);
    if (p !== 'ortho') setOrthoPatient(null);
  };

  const handleOpenOrtho = (patient) => {
    setOrthoPatient(patient);
    setPage('ortho');
  };

  if (!loggedIn) return <LoginPage onLogin={() => setLoggedIn(true)} />;

  const PAGE_TITLES = {
    dashboard: 'لوحة التحكم',
    patients: 'المرضى',
    appointments: 'المواعيد',
    ortho: 'التقويم',
    dental_chart: 'مخطط الأسنان',
    messaging: 'الرسائل',
    sms: 'تذكيرات SMS التلقائية',
    recall: 'نظام استدعاء المرضى',
    questionnaires: 'الاستبيانات الإلكترونية',
    referrals: 'طلبات الإحالة',
    vto: 'محاكاة نتيجة العلاج — VTO',
    ceph: 'التحليل السيفالومتري المتقدم',
    general: 'طب الأسنان العام',
    surgery: 'جراحة الوجه والفكين',
    finance: 'المالية والعقود',
    lab: 'طلبات المختبر',
    inventory: 'إدارة المخزون',
    reports: 'التقارير والإحصائيات',
    settings: 'إعدادات النظام',
  };

  function renderPage() {
    switch (page) {
      case 'dashboard':
        return <DashboardPage onNavigate={handleNavigate} onSelectPatient={setSelectedPatientId} />;
      case 'patients':
        return <PatientsPage selectedId={selectedPatientId} onSelectPatient={setSelectedPatientId} onOpenOrtho={handleOpenOrtho} onNewPatient={() => setShowNewPatient(true)} />;
      case 'appointments':
        return <AppointmentsPage />;
      case 'ortho':
        return <OrthoPage patient={orthoPatient} onBack={() => { setPage('patients'); setOrthoPatient(null); }} onPrescription={() => setShowPrescription(true)} />;
      case 'dental_chart':
        return <DentalChartPage />;
      case 'messaging':
        return <MessagingPage />;
      case 'sms':
        return <SMSRemindersPage />;
      case 'recall':
        return <RecallPage />;
      case 'questionnaires':
        return <QuestionnairesPage />;
      case 'referrals':
        return <ReferralsPage />;
      case 'vto':
        return <VTOPage />;
      case 'ceph':
        return <AdvancedCephPage />;
      case 'general':
        return <DentalChartPage />;
      case 'surgery':
        return <PlaceholderPage title="جراحة الوجه والفكين" icon="scalpel" desc="الحالات الجراحية، تقارير ما قبل وما بعد الجراحة، الإحالات للمستشفيات." />;
      case 'finance':
        return <FinancePage />;
      case 'lab':
        return <PlaceholderPage title="طلبات المختبر" icon="flask" desc="إدارة طلبات الأجهزة التقويمية والمختبر، تتبع الحالة والتسليم." />;
      case 'inventory':
        return <PlaceholderPage title="إدارة المخزون" icon="box" desc="مراقبة مستوى المخزون، تنبيهات النقص، طلبات الشراء." />;
      case 'reports':
        return <ReportsPage />;
      case 'settings':
        return <SettingsPage />;
      default:
        return <DashboardPage onNavigate={handleNavigate} onSelectPatient={setSelectedPatientId} />;
    }
  }

  // Tweaks Panel
  function TweaksPanel() {
    return (
      <div style={{ position: 'fixed', bottom: 24, left: 24, width: 260, background: '#fff', borderRadius: 16, boxShadow: '0 8px 32px rgba(13,33,55,0.18)', border: '1px solid #e8f0f9', zIndex: 9999, overflow: 'hidden', direction: 'rtl' }}>
        <div style={{ padding: '14px 16px', borderBottom: '1px solid #f1f5f9', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: '#0d2137' }}>
          <span style={{ fontWeight: 800, fontSize: 14, color: '#fff' }}>⚙️ Tweaks</span>
          <button onClick={() => { setShowTweaks(false); window.parent.postMessage({ type: '__edit_mode_dismissed' }, '*'); }} style={{ background: 'rgba(255,255,255,0.15)', border: 'none', borderRadius: 6, width: 24, height: 24, cursor: 'pointer', color: '#fff', fontSize: 12 }}>✕</button>
        </div>
        <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 12 }}>
          {/* Dark Mode */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 13, fontWeight: 600, color: '#0d2137' }}>🌙 الوضع الليلي</span>
            <button onClick={() => { setDarkMode(!darkMode); window.parent.postMessage({ type: '__edit_mode_set_keys', edits: { darkMode: !darkMode } }, '*'); }} style={{
              width: 44, height: 24, borderRadius: 99, border: 'none', cursor: 'pointer',
              background: darkMode ? '#3d7ab5' : '#e2e8f0', transition: 'background 0.2s', position: 'relative',
            }}>
              <div style={{ position: 'absolute', top: 3, left: darkMode ? 22 : 3, width: 18, height: 18, borderRadius: 99, background: '#fff', boxShadow: '0 1px 3px rgba(0,0,0,0.2)', transition: 'left 0.2s' }} />
            </button>
          </div>
          {/* Quick actions */}
          <div style={{ borderTop: '1px solid #f1f5f9', paddingTop: 10 }}>
            <div style={{ fontSize: 11, color: '#94a3b8', marginBottom: 8, fontWeight: 600 }}>إجراءات سريعة</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              <button onClick={() => { setShowNewPatient(true); setShowTweaks(false); }} style={{ padding: '7px 12px', borderRadius: 8, background: '#f0f5fb', border: '1px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600, color: '#3d7ab5', cursor: 'pointer', textAlign: 'right' }}>➕ مريض جديد</button>
              <button onClick={() => { setShowPrescription(true); setShowTweaks(false); }} style={{ padding: '7px 12px', borderRadius: 8, background: '#f0f5fb', border: '1px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600, color: '#a855f7', cursor: 'pointer', textAlign: 'right' }}>📋 وصفة طبية</button>
              <button onClick={() => { handleNavigate('vto'); setShowTweaks(false); }} style={{ padding: '7px 12px', borderRadius: 8, background: '#f0f5fb', border: '1px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600, color: '#3d7ab5', cursor: 'pointer', textAlign: 'right' }}>🧠 VTO محاكاة العلاج</button>
              <button onClick={() => { handleNavigate('sms'); setShowTweaks(false); }} style={{ padding: '7px 12px', borderRadius: 8, background: '#f0f5fb', border: '1px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600, color: '#64748b', cursor: 'pointer', textAlign: 'right' }}>📱 تذكيرات SMS</button>
              <button onClick={() => { handleNavigate('recall'); setShowTweaks(false); }} style={{ padding: '7px 12px', borderRadius: 8, background: '#f0f5fb', border: '1px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600, color: '#f5922e', cursor: 'pointer', textAlign: 'right' }}>🔔 استدعاء المرضى</button>
              <button onClick={() => { handleNavigate('referrals'); setShowTweaks(false); }} style={{ padding: '7px 12px', borderRadius: 8, background: '#f0f5fb', border: '1px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600, color: '#a855f7', cursor: 'pointer', textAlign: 'right' }}>↗️ الإحالات</button>
              <a href="Patient Portal.html" target="_blank" style={{ padding: '7px 12px', borderRadius: 8, background: '#0d2137', border: 'none', fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600, color: '#fff', cursor: 'pointer', textAlign: 'right', textDecoration: 'none', display: 'block' }}>📱 بوابة المريض</a>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <>
      <AppLayout page={page} onNavigate={handleNavigate} title={PAGE_TITLES[page] || 'Aqlan Dental Pro'} onNewPatient={() => setShowNewPatient(true)} onLogout={() => setLoggedIn(false)}>
        {renderPage()}
      </AppLayout>

      {showTweaks && <TweaksPanel />}
      {showNewPatient && <NewPatientModal onClose={() => setShowNewPatient(false)} onSave={(data) => console.log('New patient:', data)} />}
      {showPrescription && <PrescriptionModal patient={MOCK.patients[0]} onClose={() => setShowPrescription(false)} />}
    </>
  );
}

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(<App />);
