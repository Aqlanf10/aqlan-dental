
// ============================
// Patients Page
// ============================

function PatientsPage({ selectedId, onSelectPatient, onOpenOrtho }) {
  const [search, setSearch] = React.useState('');
  const [filter, setFilter] = React.useState('all');
  const [view, setView] = React.useState(selectedId ? 'profile' : 'list');

  React.useEffect(() => {
    if (selectedId) setView('profile');
  }, [selectedId]);

  const selected = MOCK.patients.find(p => p.id === selectedId) || MOCK.patients[0];

  const filtered = MOCK.patients.filter(p => {
    const matchType = filter === 'all' || p.type === filter;
    const matchSearch = !search || p.name.includes(search) || p.number.includes(search) || p.phone.includes(search);
    return matchType && matchSearch;
  });

  const typeColor = (t) => t === 'ortho' ? '#3d7ab5' : t === 'surgery' ? '#ef4444' : '#22c55e';
  const typeLabel = (t) => t === 'ortho' ? 'تقويم' : t === 'surgery' ? 'جراحة' : 'طب عام';

  // Patient profile view
  function PatientProfile({ patient }) {
    const [ptab, setPtab] = React.useState('info');
    const isOrtho = patient.type === 'ortho';

    return (
      <div>
        {/* Back btn */}
        <button onClick={() => setView('list')} style={{ display: 'flex', alignItems: 'center', gap: 6, background: 'none', border: 'none', cursor: 'pointer', color: '#64748b', fontFamily: 'Tajawal', fontSize: 13, marginBottom: 16, padding: 0, fontWeight: 500 }}>
          <Icon name="arrow_right" size={15} style={{ transform: 'scaleX(-1)' }} />
          العودة للقائمة
        </button>

        {/* Banner */}
        <Card style={{ marginBottom: 16 }}>
          <div style={{ display: 'flex', gap: 20, alignItems: 'center' }}>
            <div style={{ width: 64, height: 64, borderRadius: 16, background: typeColor(patient.type) + '18', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 22, fontWeight: 800, color: typeColor(patient.type), flexShrink: 0 }}>
              {patient.name[0]}
            </div>
            <div style={{ flex: 1 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 4 }}>
                <span style={{ fontSize: 20, fontWeight: 800, color: '#0d2137' }}>{patient.name}</span>
                <Badge color={typeColor(patient.type)}>{typeLabel(patient.type)}</Badge>
                <Badge color={patient.status === 'active' ? '#22c55e' : '#94a3b8'}>{patient.status === 'active' ? 'نشط' : 'غير نشط'}</Badge>
              </div>
              <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap' }}>
                {[
                  { label: 'رقم الملف', val: patient.number },
                  { label: 'العمر', val: `${patient.age} سنة` },
                  { label: 'الجنس', val: patient.gender === 'male' ? 'ذكر' : 'أنثى' },
                  { label: 'الهاتف', val: patient.phone },
                  { label: 'الطبيب', val: patient.doctor },
                  { label: 'آخر زيارة', val: patient.lastVisit },
                ].map(f => (
                  <div key={f.label} style={{ fontSize: 13 }}>
                    <span style={{ color: '#94a3b8', fontWeight: 500 }}>{f.label}: </span>
                    <span style={{ color: '#0d2137', fontWeight: 700 }}>{f.val}</span>
                  </div>
                ))}
              </div>
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              <Btn variant="outline" size="sm" icon="edit">تعديل</Btn>
              <Btn variant="orange" size="sm" icon="calendar">موعد جديد</Btn>
              {isOrtho && (
                <Btn variant="primary" size="sm" icon="tooth" onClick={() => onOpenOrtho && onOpenOrtho(patient)}>
                  ملف التقويم
                </Btn>
              )}
            </div>
          </div>
        </Card>

        {/* Tabs */}
        <Tabs
          tabs={[
            { id: 'info', label: 'البيانات الأساسية' },
            { id: 'medical', label: 'التاريخ الطبي' },
            { id: 'dental', label: 'التاريخ السني' },
            { id: 'visits', label: 'سجل الزيارات' },
            { id: 'finance', label: 'المالية' },
            { id: 'docs', label: 'المستندات' },
          ]}
          active={ptab}
          onChange={setPtab}
        />

        {ptab === 'info' && (
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Card>
              <div style={{ fontWeight: 700, fontSize: 14, color: '#0d2137', marginBottom: 16, paddingBottom: 10, borderBottom: '1px solid #f1f5f9' }}>البيانات الشخصية</div>
              {[
                ['الاسم الكامل', patient.name],
                ['رقم الملف', patient.number],
                ['تاريخ الميلاد', '1 يناير 2002'],
                ['الجنس', patient.gender === 'male' ? 'ذكر' : 'أنثى'],
                ['العنوان', 'تعز — شارع جمال'],
                ['المهنة', 'طالب'],
                ['مصدر الإحالة', 'توصية شخصية'],
              ].map(([k, v]) => (
                <div key={k} style={{ display: 'flex', justifyContent: 'space-between', padding: '7px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                  <span style={{ color: '#64748b' }}>{k}</span>
                  <span style={{ color: '#0d2137', fontWeight: 600 }}>{v}</span>
                </div>
              ))}
            </Card>
            <Card>
              <div style={{ fontWeight: 700, fontSize: 14, color: '#0d2137', marginBottom: 16, paddingBottom: 10, borderBottom: '1px solid #f1f5f9' }}>بيانات التواصل</div>
              {[
                ['الهاتف', patient.phone],
                ['واتساب', patient.phone],
                ['البريد الإلكتروني', '—'],
                ['الطبيب المعالج', patient.doctor],
                ['تاريخ التسجيل', patient.date],
                ['آخر زيارة', patient.lastVisit],
              ].map(([k, v]) => (
                <div key={k} style={{ display: 'flex', justifyContent: 'space-between', padding: '7px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                  <span style={{ color: '#64748b' }}>{k}</span>
                  <span style={{ color: '#0d2137', fontWeight: 600 }}>{v}</span>
                </div>
              ))}
            </Card>
          </div>
        )}

        {ptab === 'medical' && (
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Card>
              <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 16, paddingBottom: 10, borderBottom: '1px solid #f1f5f9' }}>التاريخ الطبي العام</div>
              {[
                ['أمراض مزمنة', 'لا يوجد'],
                ['أدوية حالية', 'لا يوجد'],
                ['حساسية دوائية', 'لا يوجد'],
                ['اضطرابات نزيف', 'لا'],
                ['حمل', 'لا ينطبق'],
                ['مشاكل مفصل الفك', 'لا'],
                ['عمليات سابقة', 'لا يوجد'],
              ].map(([k, v]) => (
                <div key={k} style={{ display: 'flex', justifyContent: 'space-between', padding: '7px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                  <span style={{ color: '#64748b' }}>{k}</span>
                  <span style={{ color: '#0d2137', fontWeight: 600 }}>{v}</span>
                </div>
              ))}
            </Card>
            <Card>
              <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 16, paddingBottom: 10, borderBottom: '1px solid #f1f5f9' }}>ملاحظات طبية</div>
              <div style={{ background: '#f7fafd', borderRadius: 8, padding: 12, fontSize: 13, color: '#64748b', lineHeight: 1.8 }}>
                لا توجد ملاحظات طبية خاصة. المريض بصحة جيدة عموماً.
              </div>
            </Card>
          </div>
        )}

        {ptab === 'dental' && (
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 16 }}>التاريخ السني</div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              {[
                ['الشكوى الرئيسية', 'ازدحام في الأسنان وتقدم الأسنان الأمامية'],
                ['العلاجات السابقة', 'حشوات متعددة، تنظيف سنوي'],
                ['التنفس الفموي', 'لا'],
                ['الطحن الليلي', 'لا'],
                ['مص الإبهام', 'لا'],
                ['ضغط اللسان', 'لا'],
              ].map(([k, v]) => (
                <div key={k} style={{ padding: '8px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                  <div style={{ color: '#94a3b8', marginBottom: 2 }}>{k}</div>
                  <div style={{ color: '#0d2137', fontWeight: 600 }}>{v}</div>
                </div>
              ))}
            </div>
          </Card>
        )}

        {ptab === 'visits' && (
          <Card padding={0}>
            <div style={{ padding: '14px 20px', borderBottom: '1px solid #f1f5f9', fontWeight: 700, fontSize: 14 }}>سجل الزيارات</div>
            {MOCK.orthoCase.visits.map((v, i) => (
              <div key={v.id} style={{ display: 'flex', gap: 14, padding: '12px 20px', borderBottom: '1px solid #f8fafc', alignItems: 'flex-start' }}>
                <div style={{ fontSize: 12, color: '#94a3b8', width: 90, flexShrink: 0, marginTop: 2 }}>{v.date}</div>
                <div>
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 4 }}>
                    <span style={{ fontWeight: 700, fontSize: 14, color: '#0d2137' }}>زيارة #{v.visitNumber} — {v.type}</span>
                    <Badge color="#3d7ab5">{v.type}</Badge>
                  </div>
                  <div style={{ fontSize: 13, color: '#64748b' }}>{v.notes}</div>
                  <div style={{ fontSize: 12, color: '#94a3b8', marginTop: 4 }}>Overjet: {v.overjet}mm · Overbite: {v.overbite}mm</div>
                </div>
              </div>
            ))}
          </Card>
        )}

        {ptab === 'finance' && (
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Card>
              <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 16 }}>ملخص مالي</div>
              {[
                ['إجمالي العقد', '1,800,000 ر.ي'],
                ['المدفوع', '1,050,000 ر.ي'],
                ['المتبقي', '750,000 ر.ي'],
                ['الأقساط', '12 قسط'],
                ['القسط الشهري', '150,000 ر.ي'],
                ['الحالة', 'نشط'],
              ].map(([k, v]) => (
                <div key={k} style={{ display: 'flex', justifyContent: 'space-between', padding: '7px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                  <span style={{ color: '#64748b' }}>{k}</span>
                  <span style={{ fontWeight: 700, color: '#0d2137' }}>{v}</span>
                </div>
              ))}
              <div style={{ marginTop: 12, height: 6, background: '#f1f5f9', borderRadius: 99, overflow: 'hidden' }}>
                <div style={{ width: '58%', height: '100%', background: '#3d7ab5', borderRadius: 99 }} />
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, color: '#94a3b8', marginTop: 4 }}>
                <span>58% محصّل</span><span>42% متبقي</span>
              </div>
            </Card>
            <Card padding={0}>
              <div style={{ padding: '14px 20px', borderBottom: '1px solid #f1f5f9', fontWeight: 700, fontSize: 14 }}>سجل الدفعات</div>
              {MOCK.finance.payments.slice(0, 4).map((p, i) => (
                <div key={p.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '10px 20px', borderBottom: '1px solid #f8fafc' }}>
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 600, color: '#0d2137' }}>{p.receipt}</div>
                    <div style={{ fontSize: 11, color: '#94a3b8' }}>{p.date} · {p.method}</div>
                  </div>
                  <div style={{ fontSize: 14, fontWeight: 700, color: '#22c55e' }}>{p.amount.toLocaleString()} ر.ي</div>
                </div>
              ))}
            </Card>
          </div>
        )}

        {ptab === 'docs' && (
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 16 }}>المستندات والموافقات</div>
            {[
              { name: 'نموذج الموافقة على العلاج', date: '15 مارس 2024', signed: true },
              { name: 'عقد العلاج التقويمي', date: '15 مارس 2024', signed: true },
              { name: 'تعليمات ما بعد التركيب', date: '15 مارس 2024', signed: false },
            ].map((d, i) => (
              <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '10px 0', borderBottom: '1px solid #f8fafc' }}>
                <Icon name="report" size={20} stroke="#3d7ab5" />
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 13, fontWeight: 600, color: '#0d2137' }}>{d.name}</div>
                  <div style={{ fontSize: 11, color: '#94a3b8' }}>{d.date}</div>
                </div>
                <Badge color={d.signed ? '#22c55e' : '#f59e0b'}>{d.signed ? 'موقّع' : 'بانتظار التوقيع'}</Badge>
                <Btn variant="ghost" size="sm" icon="eye">عرض</Btn>
              </div>
            ))}
          </Card>
        )}
      </div>
    );
  }

  // List view
  function PatientList() {
    return (
      <div>
        {/* Filters row */}
        <div style={{ display: 'flex', gap: 12, marginBottom: 16, alignItems: 'center' }}>
          <div style={{ position: 'relative', flex: 1, maxWidth: 320 }}>
            <input
              value={search}
              onChange={e => setSearch(e.target.value)}
              placeholder="بحث بالاسم أو رقم الملف أو الهاتف..."
              style={{ width: '100%', padding: '9px 38px 9px 12px', borderRadius: 10, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none', background: '#fff', direction: 'rtl', color: '#0d2137' }}
            />
            <span style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', color: '#94a3b8' }}>
              <Icon name="search" size={15} />
            </span>
          </div>
          <div style={{ display: 'flex', gap: 6 }}>
            {[['all', 'الكل'], ['ortho', 'تقويم'], ['general', 'طب عام'], ['surgery', 'جراحة']].map(([val, lbl]) => (
              <button key={val} onClick={() => setFilter(val)} style={{ padding: '7px 14px', borderRadius: 8, border: `1.5px solid ${filter === val ? '#3d7ab5' : '#dce8f5'}`, background: filter === val ? '#3d7ab5' : '#fff', color: filter === val ? '#fff' : '#64748b', fontFamily: 'Tajawal', fontSize: 13, fontWeight: 600, cursor: 'pointer' }}>
                {lbl}
              </button>
            ))}
          </div>
          <div style={{ marginRight: 'auto', display: 'flex', gap: 8 }}>
            <a href="Patient Portal.html" target="_blank" style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: '8px 16px', borderRadius: 8, background: '#0d2137', color: '#fff', fontFamily: 'Tajawal', fontSize: 14, fontWeight: 700, textDecoration: 'none', border: 'none', cursor: 'pointer' }}>
              📱 بوابة المريض
            </a>
            <Btn variant="primary" icon="plus">مريض جديد</Btn>
          </div>
        </div>

        {/* Table */}
        <Card padding={0}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr style={{ background: '#f7fafd', borderBottom: '2px solid #e8f0f9' }}>
                {['رقم الملف', 'اسم المريض', 'العمر', 'الجنس', 'التخصص', 'الطبيب المعالج', 'آخر زيارة', 'الحالة', ''].map(h => (
                  <th key={h} style={{ padding: '10px 16px', textAlign: 'right', fontWeight: 700, color: '#64748b', whiteSpace: 'nowrap', fontSize: 12 }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {filtered.map((p, i) => {
                const tc = typeColor(p.type);
                return (
                  <tr key={p.id}
                    onClick={() => { onSelectPatient(p.id); setView('profile'); }}
                    style={{ borderBottom: '1px solid #f1f5f9', cursor: 'pointer', transition: 'background 0.1s' }}
                    onMouseEnter={e => e.currentTarget.style.background = '#f7fafd'}
                    onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                  >
                    <td style={{ padding: '11px 16px', fontWeight: 700, color: '#3d7ab5' }}>{p.number}</td>
                    <td style={{ padding: '11px 16px' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                        <div style={{ width: 30, height: 30, borderRadius: 99, background: tc + '18', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 13, fontWeight: 800, color: tc, flexShrink: 0 }}>{p.name[0]}</div>
                        <span style={{ fontWeight: 600, color: '#0d2137' }}>{p.name}</span>
                      </div>
                    </td>
                    <td style={{ padding: '11px 16px', color: '#64748b' }}>{p.age}</td>
                    <td style={{ padding: '11px 16px', color: '#64748b' }}>{p.gender === 'male' ? 'ذكر' : 'أنثى'}</td>
                    <td style={{ padding: '11px 16px' }}><Badge color={tc}>{typeLabel(p.type)}</Badge></td>
                    <td style={{ padding: '11px 16px', color: '#64748b', fontSize: 12 }}>{p.doctor}</td>
                    <td style={{ padding: '11px 16px', color: '#94a3b8', fontSize: 12 }}>{p.lastVisit}</td>
                    <td style={{ padding: '11px 16px' }}><Badge color={p.status === 'active' ? '#22c55e' : '#94a3b8'}>{p.status === 'active' ? 'نشط' : 'غير نشط'}</Badge></td>
                    <td style={{ padding: '11px 16px' }}>
                      <div style={{ display: 'flex', gap: 6 }}>
                        <button style={{ padding: '4px 8px', borderRadius: 6, border: '1px solid #dce8f5', background: '#fff', cursor: 'pointer', color: '#3d7ab5', fontSize: 11, fontFamily: 'Tajawal', fontWeight: 600 }} onClick={e => { e.stopPropagation(); onSelectPatient(p.id); setView('profile'); }}>عرض</button>
                        {p.type === 'ortho' && <button style={{ padding: '4px 8px', borderRadius: 6, border: '1px solid #3d7ab5', background: '#3d7ab5', cursor: 'pointer', color: '#fff', fontSize: 11, fontFamily: 'Tajawal', fontWeight: 600 }} onClick={e => { e.stopPropagation(); onOpenOrtho && onOpenOrtho(p); }}>تقويم</button>}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <div style={{ padding: '12px 20px', borderTop: '1px solid #f1f5f9', fontSize: 12, color: '#94a3b8', display: 'flex', justifyContent: 'space-between' }}>
            <span>عرض {filtered.length} من أصل {MOCK.patients.length} مريض</span>
            <div style={{ display: 'flex', gap: 6 }}>
              {[1, 2, 3].map(n => (
                <button key={n} style={{ width: 28, height: 28, borderRadius: 6, border: '1px solid #dce8f5', background: n === 1 ? '#3d7ab5' : '#fff', color: n === 1 ? '#fff' : '#64748b', cursor: 'pointer', fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600 }}>{n}</button>
              ))}
            </div>
          </div>
        </Card>
      </div>
    );
  }

  return view === 'list' ? <PatientList /> : <PatientProfile patient={selected} />;
}

Object.assign(window, { PatientsPage });
