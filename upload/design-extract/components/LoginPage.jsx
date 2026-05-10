
// ============================
// Login Page
// ============================

function LoginPage({ onLogin }) {
  const [user, setUser] = React.useState('admin');
  const [pass, setPass] = React.useState('');
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState('');

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!pass) { setError('أدخل كلمة المرور'); return; }
    setLoading(true);
    setError('');
    setTimeout(() => { setLoading(false); onLogin(); }, 900);
  };

  return (
    <div style={{ minHeight: '100vh', background: 'linear-gradient(145deg, #0a1c30 0%, #0d2137 55%, #1a3a5c 100%)', display: 'flex', alignItems: 'center', justifyContent: 'center', direction: 'rtl', fontFamily: 'Tajawal', position: 'relative', overflow: 'hidden' }}>
      {/* Decorative circles */}
      {[...Array(3)].map((_, i) => (
        <div key={i} style={{ position: 'absolute', borderRadius: '50%', border: `1px solid rgba(61,122,181,${0.08 + i * 0.04})`, width: 300 + i * 200, height: 300 + i * 200, top: '50%', left: '50%', transform: 'translate(-50%,-50%)', pointerEvents: 'none' }} />
      ))}

      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', width: '100%', maxWidth: 420, padding: 24, zIndex: 1 }}>
        {/* Logo & Header */}
        <div style={{ textAlign: 'center', marginBottom: 32 }}>
          <div style={{ width: 90, height: 90, background: 'rgba(255,255,255,0.07)', borderRadius: 24, border: '1px solid rgba(255,255,255,0.12)', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 18px' }}>
            <img src="uploads/logo_upload-1777339394562.png" style={{ width: 70, height: 70, objectFit: 'contain' }} alt="Aqlan" />
          </div>
          <div style={{ color: '#fff', fontSize: 22, fontWeight: 800, marginBottom: 4 }}>Aqlan Dental Pro</div>
          <div style={{ color: 'rgba(255,255,255,0.5)', fontSize: 13 }}>مركز د. عقلان الكامل لطب وتقويم الأسنان</div>
          <div style={{ color: 'rgba(255,255,255,0.35)', fontSize: 12, marginTop: 4 }}>تعز — شارع التحرير الأعلى</div>
        </div>

        {/* Card */}
        <div style={{ width: '100%', background: 'rgba(255,255,255,0.05)', backdropFilter: 'blur(12px)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 20, padding: 32 }}>
          <div style={{ fontSize: 16, fontWeight: 700, color: '#fff', marginBottom: 24 }}>تسجيل الدخول</div>

          <form onSubmit={handleSubmit}>
            {/* Username */}
            <div style={{ marginBottom: 16 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: 'rgba(255,255,255,0.7)', marginBottom: 6 }}>اسم المستخدم</label>
              <input
                value={user}
                onChange={e => setUser(e.target.value)}
                style={{ width: '100%', padding: '10px 14px', borderRadius: 10, border: '1.5px solid rgba(255,255,255,0.15)', background: 'rgba(255,255,255,0.08)', color: '#fff', fontFamily: 'Tajawal', fontSize: 14, outline: 'none', direction: 'ltr', textAlign: 'right' }}
                placeholder="admin"
              />
            </div>

            {/* Password */}
            <div style={{ marginBottom: 8 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: 'rgba(255,255,255,0.7)', marginBottom: 6 }}>كلمة المرور</label>
              <input
                type="password"
                value={pass}
                onChange={e => setPass(e.target.value)}
                style={{ width: '100%', padding: '10px 14px', borderRadius: 10, border: '1.5px solid rgba(255,255,255,0.15)', background: 'rgba(255,255,255,0.08)', color: '#fff', fontFamily: 'Tajawal', fontSize: 14, outline: 'none' }}
                placeholder="••••••••"
              />
            </div>

            {error && <div style={{ color: '#fca5a5', fontSize: 13, marginBottom: 12 }}>{error}</div>}

            <div style={{ textAlign: 'left', marginBottom: 20 }}>
              <a href="#" style={{ fontSize: 12, color: '#f5922e', textDecoration: 'none' }}>نسيت كلمة المرور؟</a>
            </div>

            <button type="submit" disabled={loading} style={{ width: '100%', padding: '12px', borderRadius: 10, background: loading ? '#2d5e8e' : '#3d7ab5', color: '#fff', border: 'none', fontFamily: 'Tajawal', fontSize: 15, fontWeight: 700, cursor: loading ? 'wait' : 'pointer', transition: 'background 0.2s', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8 }}>
              {loading ? (
                <>
                  <div style={{ width: 16, height: 16, border: '2px solid rgba(255,255,255,0.3)', borderTopColor: '#fff', borderRadius: 99, animation: 'spin 0.7s linear infinite' }} />
                  جارٍ الدخول...
                </>
              ) : 'دخول'}
            </button>
          </form>
        </div>

        {/* Doctors */}
        <div style={{ marginTop: 28, width: '100%' }}>
          <div style={{ color: 'rgba(255,255,255,0.35)', fontSize: 12, textAlign: 'center', marginBottom: 12 }}>الفريق الطبي</div>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'center', flexWrap: 'wrap' }}>
            {MOCK.doctors.map(d => (
              <div key={d.id} title={d.name} style={{ width: 36, height: 36, borderRadius: 99, background: d.color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700, color: '#fff', border: '2px solid rgba(255,255,255,0.15)', cursor: 'default' }}>
                {d.initials}
              </div>
            ))}
          </div>
        </div>

        {/* Footer */}
        <div style={{ marginTop: 32, color: 'rgba(255,255,255,0.2)', fontSize: 11, textAlign: 'center' }}>
          04-253028 · 770-245745 · 711-752823
        </div>
      </div>

      <style>{`
        @keyframes spin { to { transform: rotate(360deg); } }
        input::placeholder { color: rgba(255,255,255,0.3) !important; }
        input:focus { border-color: rgba(61,122,181,0.7) !important; }
      `}</style>
    </div>
  );
}

Object.assign(window, { LoginPage });
