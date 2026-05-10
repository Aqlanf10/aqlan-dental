
// ============================
// Dashboard Page
// ============================

function DashboardPage({ onNavigate, onSelectPatient }) {
  const s = MOCK.stats;

  // Mini bar chart
  function MiniChart() {
    const max = Math.max(...MOCK.finance.monthly.map(m => m.amount));
    return (
      <div style={{ display: 'flex', alignItems: 'flex-end', gap: 6, height: 60, padding: '0 4px' }}>
        {MOCK.finance.monthly.map((m, i) => {
          const h = Math.round((m.amount / max) * 52);
          const isLast = i === MOCK.finance.monthly.length - 1;
          return (
            <div key={m.month} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
              <div style={{ width: '100%', height: h, background: isLast ? '#3d7ab5' : '#dce8f5', borderRadius: '4px 4px 0 0', transition: 'height 0.3s' }} />
              <span style={{ fontSize: 9, color: '#94a3b8', whiteSpace: 'nowrap' }}>{m.month}</span>
            </div>
          );
        })}
      </div>
    );
  }

  // Doctor performance row
  function DoctorRow({ doctor }) {
    const pct = Math.round((doctor.patients / 312) * 100);
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '8px 0', borderBottom: '1px solid #f1f5f9' }}>
        <div style={{ width: 34, height: 34, borderRadius: 99, background: doctor.color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700, color: '#fff', flexShrink: 0 }}>{doctor.initials}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 13, fontWeight: 600, color: '#0d2137', marginBottom: 3 }}>{doctor.name}</div>
          <div style={{ height: 5, background: '#f1f5f9', borderRadius: 99, overflow: 'hidden' }}>
            <div style={{ height: '100%', width: `${pct}%`, background: doctor.color, borderRadius: 99 }} />
          </div>
        </div>
        <div style={{ fontSize: 12, color: '#64748b', fontWeight: 600 }}>{doctor.patients} مريض</div>
      </div>
    );
  }

  const statusColors = { completed: '#22c55e', in_progress: '#3d7ab5', arrived: '#f5922e', scheduled: '#94a3b8' };

  return (
    <div>
      {/* Stats row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16, marginBottom: 20 }}>
        <StatCard label="إجمالي المرضى" value={s.totalPatients.toLocaleString()} icon="users" color="#3d7ab5" sub={`+${s.newPatientsMonth} هذا الشهر`} />
        <StatCard label="مواعيد اليوم" value={s.todayAppointments} icon="calendar" color="#f5922e" sub={`${s.completedToday} مكتمل`} />
        <StatCard label="حالات تقويم نشطة" value={s.activeOrtho} icon="tooth" color="#a855f7" sub="جارية حالياً" />
        <StatCard label="إيرادات الشهر" value={`${(s.monthlyRevenue / 1000).toFixed(0)}K ر.ي`} icon="money" color="#22c55e" sub={`متأخرات: ${(s.pendingPayments / 1000).toFixed(0)}K`} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 380px', gap: 16, marginBottom: 16 }}>
        {/* Today appointments */}
        <Card padding={0}>
          <div style={{ padding: '16px 20px', borderBottom: '1px solid #f1f5f9', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ fontWeight: 800, fontSize: 15, color: '#0d2137' }}>مواعيد اليوم — الإثنين 28 أكتوبر</div>
            <Btn variant="ghost" size="sm" icon="arrow_right" onClick={() => onNavigate('appointments')}>عرض الكل</Btn>
          </div>
          <div>
            {MOCK.todayAppointments.map((a, i) => {
              const st = apptStatusLabel(a.status);
              return (
                <div key={a.id}
                  onClick={() => { onSelectPatient(a.patientId); onNavigate('patients'); }}
                  style={{ display: 'flex', alignItems: 'center', gap: 14, padding: '11px 20px', borderBottom: i < MOCK.todayAppointments.length - 1 ? '1px solid #f8fafc' : 'none', cursor: 'pointer', transition: 'background 0.1s' }}
                  onMouseEnter={e => e.currentTarget.style.background = '#f7fafd'}
                  onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                >
                  <div style={{ fontSize: 12, fontWeight: 700, color: '#64748b', width: 38, textAlign: 'center', flexShrink: 0 }}>{a.time}</div>
                  <div style={{ width: 3, height: 32, borderRadius: 99, background: a.doctorColor, flexShrink: 0 }} />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: 14, fontWeight: 700, color: '#0d2137' }}>{a.patient}</div>
                    <div style={{ fontSize: 12, color: '#94a3b8', marginTop: 1 }}>{a.type} · {a.doctor}</div>
                  </div>
                  <Badge color={st.color}>{st.label}</Badge>
                  <div style={{ fontSize: 11, color: '#cbd5e1' }}>{a.duration} دق</div>
                </div>
              );
            })}
          </div>
        </Card>

        {/* Revenue chart + quick stats */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card>
            <div style={{ fontWeight: 800, fontSize: 14, color: '#0d2137', marginBottom: 4 }}>الإيراد الشهري (ألف ر.ي)</div>
            <div style={{ fontSize: 22, fontWeight: 800, color: '#0d2137', marginBottom: 12 }}>2,840K <span style={{ fontSize: 13, fontWeight: 500, color: '#22c55e' }}>↑ 23%</span></div>
            <MiniChart />
          </Card>

          <Card>
            <div style={{ fontWeight: 800, fontSize: 14, color: '#0d2137', marginBottom: 12 }}>أداء الأطباء</div>
            {MOCK.doctors.map(d => <DoctorRow key={d.id} doctor={d} />)}
          </Card>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
        {/* Recent patients */}
        <Card padding={0}>
          <div style={{ padding: '16px 20px', borderBottom: '1px solid #f1f5f9', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ fontWeight: 800, fontSize: 15 }}>أحدث المرضى</div>
            <Btn variant="ghost" size="sm" onClick={() => onNavigate('patients')}>عرض الكل</Btn>
          </div>
          {MOCK.patients.slice(0, 5).map((p, i) => {
            const typeColor = p.type === 'ortho' ? '#3d7ab5' : p.type === 'surgery' ? '#ef4444' : '#22c55e';
            const typeLabel = p.type === 'ortho' ? 'تقويم' : p.type === 'surgery' ? 'جراحة' : 'طب عام';
            return (
              <div key={p.id}
                onClick={() => { onSelectPatient(p.id); onNavigate('patients'); }}
                style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '10px 20px', borderBottom: i < 4 ? '1px solid #f8fafc' : 'none', cursor: 'pointer' }}
                onMouseEnter={e => e.currentTarget.style.background = '#f7fafd'}
                onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
              >
                <div style={{ width: 34, height: 34, borderRadius: 99, background: typeColor + '18', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 13, fontWeight: 700, color: typeColor, flexShrink: 0 }}>
                  {p.name[0]}
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 13, fontWeight: 700, color: '#0d2137' }}>{p.name}</div>
                  <div style={{ fontSize: 11, color: '#94a3b8' }}>{p.number} · {p.doctor}</div>
                </div>
                <Badge color={typeColor}>{typeLabel}</Badge>
              </div>
            );
          })}
        </Card>

        {/* Quick actions + recent payments */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card>
            <div style={{ fontWeight: 800, fontSize: 14, color: '#0d2137', marginBottom: 14 }}>إجراءات سريعة</div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
              {[
                { label: 'مريض جديد', icon: 'plus', color: '#3d7ab5', page: 'patients' },
                { label: 'موعد جديد', icon: 'calendar', color: '#f5922e', page: 'appointments' },
                { label: 'حالة تقويم', icon: 'tooth', color: '#a855f7', page: 'ortho' },
                { label: 'تسجيل دفعة', icon: 'money', color: '#22c55e', page: 'finance' },
              ].map(a => (
                <button key={a.label} onClick={() => onNavigate(a.page)} style={{ padding: '12px', borderRadius: 10, background: a.color + '10', border: `1.5px solid ${a.color}25`, display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontFamily: 'Tajawal', fontSize: 13, fontWeight: 600, color: a.color, transition: 'background 0.15s' }}
                  onMouseEnter={e => e.currentTarget.style.background = a.color + '20'}
                  onMouseLeave={e => e.currentTarget.style.background = a.color + '10'}
                >
                  <Icon name={a.icon} size={16} stroke={a.color} />
                  {a.label}
                </button>
              ))}
              <a href="Patient Portal.html" target="_blank" style={{ padding: '12px', borderRadius: 10, background: '#0d2137', border: '1.5px solid #1a3a5c', display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontFamily: 'Tajawal', fontSize: 13, fontWeight: 600, color: '#fff', textDecoration: 'none', gridColumn: 'span 2', justifyContent: 'center' }}>
                📱 فتح بوابة المريض
              </a>
            </div>
          </Card>

          <Card padding={0}>
            <div style={{ padding: '14px 20px', borderBottom: '1px solid #f1f5f9', fontWeight: 800, fontSize: 14 }}>آخر المدفوعات</div>
            {MOCK.finance.payments.slice(0, 4).map((p, i) => (
              <div key={p.id} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '9px 20px', borderBottom: i < 3 ? '1px solid #f8fafc' : 'none' }}>
                <div style={{ width: 8, height: 8, borderRadius: 99, background: '#22c55e', flexShrink: 0, marginTop: 2 }} />
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 13, fontWeight: 600, color: '#0d2137' }}>{p.patient}</div>
                  <div style={{ fontSize: 11, color: '#94a3b8' }}>{p.date} · {p.specialty}</div>
                </div>
                <div style={{ fontSize: 13, fontWeight: 700, color: '#22c55e' }}>{p.amount.toLocaleString()} ر.ي</div>
              </div>
            ))}
          </Card>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { DashboardPage });
