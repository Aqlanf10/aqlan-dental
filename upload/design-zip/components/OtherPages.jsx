
// ============================
// Appointments Page
// ============================
function AppointmentsPage() {
  const [view, setView] = React.useState('day');
  const days = ['الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس'];
  const hours = ['08:00','09:00','10:00','11:00','12:00','13:00','14:00','15:00','16:00'];

  const appts = MOCK.todayAppointments;

  function DayView() {
    return (
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 320px', gap: 16 }}>
        <Card padding={0}>
          <div style={{ padding: '14px 20px', borderBottom: '1px solid #f1f5f9', display: 'flex', gap: 10, alignItems: 'center' }}>
            <div style={{ fontWeight: 800, fontSize: 15, flex: 1 }}>الإثنين — 28 أكتوبر 2024</div>
            <div style={{ display: 'flex', gap: 6, fontSize: 12 }}>
              {MOCK.doctors.map(d => (
                <div key={d.id} title={d.name} style={{ width: 28, height: 28, borderRadius: 99, background: d.color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11, fontWeight: 700, color: '#fff', cursor: 'pointer', border: '2px solid #fff' }}>
                  {d.initials}
                </div>
              ))}
            </div>
          </div>
          <div style={{ overflowY: 'auto', maxHeight: 540 }}>
            {appts.map((a, i) => {
              const st = apptStatusLabel(a.status);
              return (
                <div key={a.id} style={{ display: 'flex', gap: 0, borderBottom: '1px solid #f8fafc' }}>
                  <div style={{ width: 56, padding: '14px 8px', textAlign: 'center', fontSize: 12, fontWeight: 700, color: '#94a3b8', flexShrink: 0, borderLeft: '1px solid #f1f5f9' }}>
                    {a.time}
                  </div>
                  <div style={{ flex: 1, padding: '10px 14px', display: 'flex', gap: 10, alignItems: 'center', background: a.status === 'in_progress' ? '#f0f5fb' : 'transparent' }}>
                    <div style={{ width: 4, height: 44, borderRadius: 99, background: a.doctorColor, flexShrink: 0 }} />
                    <div style={{ flex: 1 }}>
                      <div style={{ fontWeight: 700, fontSize: 14, color: '#0d2137', marginBottom: 2 }}>{a.patient}</div>
                      <div style={{ fontSize: 12, color: '#94a3b8' }}>{a.type} · {a.doctor} · {a.duration} دق</div>
                    </div>
                    <Badge color={st.color}>{st.label}</Badge>
                  </div>
                </div>
              );
            })}
          </div>
        </Card>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>إحصائيات اليوم</div>
            {[
              { label: 'إجمالي المواعيد', val: appts.length, color: '#3d7ab5' },
              { label: 'مكتملة', val: appts.filter(a => a.status === 'completed').length, color: '#22c55e' },
              { label: 'جارية', val: appts.filter(a => a.status === 'in_progress').length, color: '#3d7ab5' },
              { label: 'قادمة', val: appts.filter(a => a.status === 'scheduled').length, color: '#f5922e' },
            ].map(s => (
              <div key={s.label} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '7px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                <span style={{ color: '#64748b' }}>{s.label}</span>
                <span style={{ fontWeight: 800, color: s.color, fontSize: 16 }}>{s.val}</span>
              </div>
            ))}
          </Card>

          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>توزيع الأطباء اليوم</div>
            {MOCK.doctors.map(d => {
              const count = appts.filter(a => a.doctor === d.name).length;
              return count > 0 ? (
                <div key={d.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '7px 0', borderBottom: '1px solid #f8fafc' }}>
                  <div style={{ width: 28, height: 28, borderRadius: 99, background: d.color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11, fontWeight: 700, color: '#fff', flexShrink: 0 }}>{d.initials}</div>
                  <span style={{ flex: 1, fontSize: 13, color: '#0d2137' }}>{d.name}</span>
                  <Badge color={d.color}>{count} موعد</Badge>
                </div>
              ) : null;
            })}
          </Card>

          <Btn variant="primary" icon="plus" style={{ width: '100%', justifyContent: 'center' }}>موعد جديد</Btn>
        </div>
      </div>
    );
  }

  function WeekView() {
    return (
      <Card padding={0}>
        <div style={{ display: 'grid', gridTemplateColumns: '56px repeat(5, 1fr)' }}>
          <div style={{ background: '#f7fafd', borderBottom: '2px solid #e8f0f9' }} />
          {days.map(d => (
            <div key={d} style={{ padding: '12px 8px', textAlign: 'center', fontWeight: 700, fontSize: 13, color: '#64748b', background: '#f7fafd', borderBottom: '2px solid #e8f0f9', borderRight: '1px solid #f1f5f9' }}>
              {d}
            </div>
          ))}
          {hours.map(h => (
            <React.Fragment key={h}>
              <div style={{ padding: '12px 4px', textAlign: 'center', fontSize: 11, color: '#94a3b8', borderBottom: '1px solid #f1f5f9', borderLeft: '1px solid #f1f5f9', fontWeight: 700 }}>{h}</div>
              {days.map((d, di) => {
                const slot = di === 1 ? appts.find(a => a.time.startsWith(h.split(':')[0])) : null;
                return (
                  <div key={d} style={{ padding: '4px', borderBottom: '1px solid #f1f5f9', borderRight: '1px solid #f1f5f9', minHeight: 44, position: 'relative' }}>
                    {slot && (
                      <div style={{ background: slot.doctorColor + '22', border: `1.5px solid ${slot.doctorColor}50`, borderRadius: 6, padding: '4px 8px', fontSize: 11, fontWeight: 600, color: slot.doctorColor, lineHeight: 1.4 }}>
                        <div>{slot.patient.split(' ')[0]}</div>
                        <div style={{ fontWeight: 400, opacity: 0.8 }}>{slot.type}</div>
                      </div>
                    )}
                  </div>
                );
              })}
            </React.Fragment>
          ))}
        </div>
      </Card>
    );
  }

  return (
    <div>
      <div style={{ display: 'flex', gap: 10, marginBottom: 16, alignItems: 'center' }}>
        <div style={{ display: 'flex', gap: 4, background: '#f0f5fb', padding: 4, borderRadius: 10 }}>
          {[['day','اليوم'],['week','الأسبوع']].map(([val,lbl]) => (
            <button key={val} onClick={() => setView(val)} style={{ padding: '6px 16px', borderRadius: 7, border: 'none', background: view === val ? '#fff' : 'transparent', color: view === val ? '#0d2137' : '#64748b', fontFamily: 'Tajawal', fontWeight: 700, fontSize: 13, cursor: 'pointer', boxShadow: view === val ? '0 1px 4px rgba(0,0,0,0.08)' : 'none' }}>
              {lbl}
            </button>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <button style={{ padding: '6px 12px', borderRadius: 8, border: '1px solid #dce8f5', background: '#fff', cursor: 'pointer', fontFamily: 'Tajawal', fontSize: 13, color: '#64748b' }}>‹</button>
          <span style={{ fontWeight: 700, fontSize: 14, color: '#0d2137' }}>أكتوبر 2024</span>
          <button style={{ padding: '6px 12px', borderRadius: 8, border: '1px solid #dce8f5', background: '#fff', cursor: 'pointer', fontFamily: 'Tajawal', fontSize: 13, color: '#64748b' }}>›</button>
        </div>
        <div style={{ marginRight: 'auto' }}>
          <Btn variant="primary" icon="plus">موعد جديد</Btn>
        </div>
      </div>
      {view === 'day' ? <DayView /> : <WeekView />}
    </div>
  );
}

// ============================
// Finance Page
// ============================
function FinancePage() {
  const [ftab, setFtab] = React.useState('overview');
  const f = MOCK.finance;

  function Overview() {
    const max = Math.max(...f.monthly.map(m => m.amount));
    return (
      <div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4,1fr)', gap: 14, marginBottom: 18 }}>
          {[
            { label: 'إيراد أكتوبر', val: '2,840,000 ر.ي', color: '#22c55e', icon: 'money', sub: '↑ 23%' },
            { label: 'المحصّل', val: '2,390,000 ر.ي', color: '#3d7ab5', icon: 'check', sub: '84% من الإيراد' },
            { label: 'المتأخرات', val: '450,000 ر.ي', color: '#ef4444', icon: 'clock', sub: '12 عقد متأخر' },
            { label: 'العقود النشطة', val: f.contracts.length, color: '#a855f7', icon: 'report', sub: 'تقويم + جراحة' },
          ].map(s => <StatCard key={s.label} label={s.label} value={s.val} icon={s.icon} color={s.color} sub={s.sub} />)}
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
          <Card>
            <div style={{ fontWeight: 700, fontSize: 15, marginBottom: 16 }}>الإيراد الشهري (ألف ر.ي)</div>
            <div style={{ display: 'flex', alignItems: 'flex-end', gap: 10, height: 100 }}>
              {f.monthly.map((m, i) => {
                const h = Math.round((m.amount / max) * 85);
                const isLast = i === f.monthly.length - 1;
                return (
                  <div key={m.month} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
                    <div style={{ fontSize: 11, fontWeight: 700, color: isLast ? '#3d7ab5' : '#94a3b8' }}>{m.amount}</div>
                    <div style={{ width: '100%', height: h, background: isLast ? '#3d7ab5' : '#dce8f5', borderRadius: '6px 6px 0 0', transition: 'height 0.4s' }} />
                    <div style={{ fontSize: 11, color: '#94a3b8' }}>{m.month}</div>
                  </div>
                );
              })}
            </div>
          </Card>

          <Card>
            <div style={{ fontWeight: 700, fontSize: 15, marginBottom: 16 }}>توزيع الإيراد بالتخصص</div>
            {[
              { label: 'التقويم', pct: 58, color: '#3d7ab5', amount: '1,648K' },
              { label: 'طب الأسنان العام', pct: 28, color: '#22c55e', amount: '795K' },
              { label: 'جراحة الوجه والفكين', pct: 14, color: '#ef4444', amount: '397K' },
            ].map(s => (
              <div key={s.label} style={{ marginBottom: 14 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginBottom: 5 }}>
                  <span style={{ fontWeight: 600, color: '#0d2137' }}>{s.label}</span>
                  <span style={{ color: '#64748b' }}>{s.amount} ر.ي · {s.pct}%</span>
                </div>
                <div style={{ height: 7, background: '#f1f5f9', borderRadius: 99, overflow: 'hidden' }}>
                  <div style={{ width: `${s.pct}%`, height: '100%', background: s.color, borderRadius: 99 }} />
                </div>
              </div>
            ))}
          </Card>
        </div>

        <Card padding={0}>
          <div style={{ padding: '14px 20px', borderBottom: '1px solid #f1f5f9', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div style={{ fontWeight: 700, fontSize: 15 }}>آخر الدفعات</div>
            <Btn variant="primary" size="sm" icon="plus">تسجيل دفعة</Btn>
          </div>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr style={{ background: '#f7fafd', borderBottom: '1px solid #e8f0f9' }}>
                {['رقم الإيصال','المريض','التاريخ','التخصص','طريقة الدفع','المبلغ',''].map(h => (
                  <th key={h} style={{ padding: '9px 16px', textAlign: 'right', fontWeight: 700, color: '#64748b', fontSize: 12 }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {f.payments.map(p => (
                <tr key={p.id} style={{ borderBottom: '1px solid #f8fafc' }} onMouseEnter={e => e.currentTarget.style.background = '#f7fafd'} onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
                  <td style={{ padding: '10px 16px', fontWeight: 700, color: '#3d7ab5' }}>{p.receipt}</td>
                  <td style={{ padding: '10px 16px', fontWeight: 600, color: '#0d2137' }}>{p.patient}</td>
                  <td style={{ padding: '10px 16px', color: '#64748b' }}>{p.date}</td>
                  <td style={{ padding: '10px 16px' }}><Badge color="#3d7ab5">{p.specialty}</Badge></td>
                  <td style={{ padding: '10px 16px' }}><Badge color="#64748b">{p.method}</Badge></td>
                  <td style={{ padding: '10px 16px', fontWeight: 800, color: '#22c55e' }}>{p.amount.toLocaleString()} ر.ي</td>
                  <td style={{ padding: '10px 16px' }}>
                    <Btn variant="ghost" size="sm" icon="eye">سند</Btn>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      </div>
    );
  }

  function ContractsTab() {
    return (
      <div>
        <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 14 }}>
          <Btn variant="primary" icon="plus">عقد جديد</Btn>
        </div>
        {f.contracts.map(c => {
          const pct = Math.round((c.paid / c.total) * 100);
          return (
            <Card key={c.id} style={{ marginBottom: 12 }}>
              <div style={{ display: 'flex', gap: 14, alignItems: 'center', marginBottom: 12 }}>
                <div style={{ flex: 1 }}>
                  <div style={{ fontWeight: 700, fontSize: 15, color: '#0d2137', marginBottom: 4 }}>{c.patient}</div>
                  <div style={{ display: 'flex', gap: 10 }}>
                    <Badge color="#3d7ab5">{c.specialty}</Badge>
                    <Badge color="#22c55e">نشط</Badge>
                    <span style={{ fontSize: 12, color: '#94a3b8' }}>{c.installments} قسط</span>
                  </div>
                </div>
                <div style={{ textAlign: 'left' }}>
                  <div style={{ fontSize: 11, color: '#94a3b8' }}>إجمالي العقد</div>
                  <div style={{ fontSize: 18, fontWeight: 800, color: '#0d2137' }}>{c.total.toLocaleString()} ر.ي</div>
                </div>
              </div>
              <div style={{ display: 'flex', gap: 20, marginBottom: 10, fontSize: 13 }}>
                <div><span style={{ color: '#94a3b8' }}>المحصّل: </span><span style={{ fontWeight: 700, color: '#22c55e' }}>{c.paid.toLocaleString()}</span></div>
                <div><span style={{ color: '#94a3b8' }}>المتبقي: </span><span style={{ fontWeight: 700, color: '#f5922e' }}>{(c.total - c.paid).toLocaleString()}</span></div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#94a3b8', marginBottom: 4 }}>
                <span>التحصيل</span><span>{pct}%</span>
              </div>
              <div style={{ height: 6, background: '#f1f5f9', borderRadius: 99, overflow: 'hidden' }}>
                <div style={{ width: `${pct}%`, height: '100%', background: '#3d7ab5', borderRadius: 99 }} />
              </div>
            </Card>
          );
        })}
      </div>
    );
  }

  return (
    <div>
      <Tabs tabs={[{id:'overview',label:'نظرة عامة'},{id:'contracts',label:'العقود'},{id:'overdue',label:'المتأخرات'}]} active={ftab} onChange={setFtab} />
      {ftab === 'overview' && <Overview />}
      {ftab === 'contracts' && <ContractsTab />}
      {ftab === 'overdue' && (
        <Card>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14, color: '#ef4444' }}>المدفوعات المتأخرة</div>
          {[
            { patient: 'محمد سالم الحمادي', amount: 200000, overdue: '45 يوم', specialty: 'طب عام' },
            { patient: 'عبدالرحمن السعدي', amount: 150000, overdue: '30 يوم', specialty: 'طب عام' },
            { patient: 'خالد المروني', amount: 100000, overdue: '15 يوم', specialty: 'تقويم' },
          ].map((r, i) => (
            <div key={i} style={{ display: 'flex', gap: 14, alignItems: 'center', padding: '10px 0', borderBottom: '1px solid #f8fafc' }}>
              <div style={{ width: 8, height: 8, borderRadius: 99, background: '#ef4444', flexShrink: 0 }} />
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 700, fontSize: 13, color: '#0d2137' }}>{r.patient}</div>
                <div style={{ fontSize: 11, color: '#94a3b8' }}>متأخرة منذ {r.overdue} · {r.specialty}</div>
              </div>
              <div style={{ fontWeight: 700, color: '#ef4444' }}>{r.amount.toLocaleString()} ر.ي</div>
              <Btn variant="ghost" size="sm">إشعار</Btn>
            </div>
          ))}
        </Card>
      )}
    </div>
  );
}

// ============================
// Reports Page
// ============================
function ReportsPage() {
  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 14, marginBottom: 20 }}>
        {[
          { label: 'المرضى الجدد - أكتوبر', val: 23, sub: '↑ 15%', color: '#3d7ab5' },
          { label: 'نسبة الحضور', val: '87%', sub: '−3% عن الشهر الماضي', color: '#f5922e' },
          { label: 'حالات تقويم مكتملة YTD', val: 41, sub: 'من يناير', color: '#22c55e' },
        ].map(s => <StatCard key={s.label} label={s.label} value={s.val} icon="chart" color={s.color} sub={s.sub} />)}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
        {[
          { title: 'تقرير المرضى الشهري', icon: 'users', desc: 'إجمالي المرضى، الجدد، التوزيع حسب التخصص' },
          { title: 'تقرير الحالات التقويمية', icon: 'tooth', desc: 'الحالات النشطة، المكتملة، المراحل' },
          { title: 'تقرير الأداء المالي', icon: 'money', desc: 'الإيرادات، التحصيل، المتأخرات' },
          { title: 'تقرير أداء الأطباء', icon: 'user', desc: 'عدد المرضى، الزيارات، التقييم' },
          { title: 'تقرير المواعيد', icon: 'calendar', desc: 'معدل الحضور، الإلغاءات، قائمة الانتظار' },
          { title: 'تقرير المختبر والمخزون', icon: 'flask', desc: 'طلبات المختبر، المخزون المنخفض' },
        ].map(r => (
          <Card key={r.title} style={{ cursor: 'pointer', transition: 'box-shadow 0.15s' }}
            onMouseEnter={e => e.currentTarget.style.boxShadow = '0 4px 20px rgba(61,122,181,0.12)'}
            onMouseLeave={e => e.currentTarget.style.boxShadow = '0 1px 3px rgba(13,33,55,0.06)'}
          >
            <div style={{ display: 'flex', gap: 14, alignItems: 'center', marginBottom: 10 }}>
              <div style={{ width: 40, height: 40, borderRadius: 10, background: '#3d7ab515', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <Icon name={r.icon} size={20} stroke="#3d7ab5" />
              </div>
              <div>
                <div style={{ fontWeight: 700, fontSize: 14, color: '#0d2137' }}>{r.title}</div>
                <div style={{ fontSize: 12, color: '#94a3b8', marginTop: 2 }}>{r.desc}</div>
              </div>
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              <Btn variant="outline" size="sm" icon="eye">عرض</Btn>
              <Btn variant="ghost" size="sm" icon="report">PDF</Btn>
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}

// ============================
// Generic placeholder pages
// ============================
function PlaceholderPage({ title, icon, desc }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: 400, gap: 16 }}>
      <div style={{ width: 64, height: 64, borderRadius: 16, background: '#3d7ab515', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <Icon name={icon} size={28} stroke="#3d7ab5" />
      </div>
      <div style={{ fontSize: 20, fontWeight: 800, color: '#0d2137' }}>{title}</div>
      <div style={{ fontSize: 14, color: '#94a3b8', maxWidth: 340, textAlign: 'center', lineHeight: 1.7 }}>{desc}</div>
      <Btn variant="primary" icon="plus">إضافة جديد</Btn>
    </div>
  );
}

// ============================
// Settings Page
// ============================
function SettingsPage() {
  const [stab, setStab] = React.useState('center');
  return (
    <div>
      <Tabs tabs={[{id:'center',label:'بيانات المركز'},{id:'users',label:'المستخدمون'},{id:'permissions',label:'الصلاحيات'},{id:'backup',label:'النسخ الاحتياطي'}]} active={stab} onChange={setStab} />
      {stab === 'center' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 16 }}>معلومات المركز</div>
            {[
              ['اسم المركز', 'مركز د. عقلان الكامل لطب وتقويم الأسنان'],
              ['الموقع', 'اليمن — تعز — شارع التحرير الأعلى'],
              ['هاتف 1', '04-253028'],
              ['هاتف 2', '770-245745'],
              ['هاتف 3', '711-752823'],
              ['العملة', 'ريال يمني (YER)'],
            ].map(([k, v]) => (
              <div key={k} style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                <span style={{ color: '#64748b' }}>{k}</span>
                <span style={{ fontWeight: 600, color: '#0d2137', maxWidth: 260, textAlign: 'left' }}>{v}</span>
              </div>
            ))}
            <div style={{ marginTop: 16 }}>
              <Btn variant="primary" size="sm" icon="edit">تعديل</Btn>
            </div>
          </Card>
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 16 }}>الفريق الطبي</div>
            {MOCK.doctors.map(d => (
              <div key={d.id} style={{ display: 'flex', gap: 12, alignItems: 'center', padding: '8px 0', borderBottom: '1px solid #f8fafc' }}>
                <div style={{ width: 34, height: 34, borderRadius: 99, background: d.color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700, color: '#fff', flexShrink: 0 }}>{d.initials}</div>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 13, fontWeight: 700, color: '#0d2137' }}>{d.name}</div>
                  <div style={{ fontSize: 11, color: '#94a3b8' }}>{d.specialty}</div>
                </div>
                <Btn variant="ghost" size="sm" icon="edit"></Btn>
              </div>
            ))}
          </Card>
        </div>
      )}
      {stab === 'users' && (
        <Card>
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 14 }}>
            <Btn variant="primary" icon="plus">مستخدم جديد</Btn>
          </div>
          {[
            { name: 'د. عقلان الكامل', role: 'مدير النظام', initials: 'عك', color: '#3d7ab5', last: 'الآن' },
            { name: 'ريم (استقبال)', role: 'موظفة استقبال', initials: 'ري', color: '#f5922e', last: 'منذ ساعة' },
            { name: 'خالد (محاسب)', role: 'محاسب', initials: 'خا', color: '#22c55e', last: 'أمس' },
          ].map((u, i) => (
            <div key={i} style={{ display: 'flex', gap: 12, alignItems: 'center', padding: '10px 0', borderBottom: '1px solid #f8fafc' }}>
              <div style={{ width: 36, height: 36, borderRadius: 99, background: u.color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 13, fontWeight: 700, color: '#fff', flexShrink: 0 }}>{u.initials}</div>
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: 13, fontWeight: 700, color: '#0d2137' }}>{u.name}</div>
                <div style={{ fontSize: 11, color: '#94a3b8' }}>{u.role} · آخر دخول: {u.last}</div>
              </div>
              <Badge color="#22c55e">نشط</Badge>
              <Btn variant="ghost" size="sm" icon="edit"></Btn>
            </div>
          ))}
        </Card>
      )}
      {(stab === 'permissions' || stab === 'backup') && (
        <PlaceholderPage title={stab === 'permissions' ? 'إدارة الصلاحيات' : 'النسخ الاحتياطي'} icon="settings" desc="هذا القسم قيد التطوير" />
      )}
    </div>
  );
}

Object.assign(window, { AppointmentsPage, FinancePage, ReportsPage, SettingsPage, PlaceholderPage });
