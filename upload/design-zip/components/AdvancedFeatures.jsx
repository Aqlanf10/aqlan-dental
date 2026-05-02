
// ============================
// Feature 1: SMS Reminders System
// ============================

function SMSRemindersPage() {
  const [templates, setTemplates] = React.useState([
    { id: 1, name: 'تذكير الموعد — 48 ساعة', active: true, timing: '48h', type: 'appointment', message: 'عزيزي {اسم_المريض}، نذكرك بموعدك في مركز د. عقلان الكامل يوم {اليوم} الساعة {الوقت}. للإلغاء أو التعديل: 770-245745' },
    { id: 2, name: 'تذكير الموعد — 2 ساعة', active: true, timing: '2h', type: 'appointment', message: 'تذكير: موعدك اليوم الساعة {الوقت} في مركز د. عقلان. نتطلع لرؤيتك! 😊' },
    { id: 3, name: 'تأكيد الحجز', active: true, timing: 'immediate', type: 'confirmation', message: 'تم تأكيد موعدك: {اليوم} الساعة {الوقت} — د. {اسم_الطبيب}. رقم الحجز: {رقم_الحجز}' },
    { id: 4, name: 'متابعة ما بعد الزيارة', active: false, timing: '24h_after', type: 'followup', message: 'نأمل أن تكون بخير بعد زيارتك اليوم. إذا واجهتك أي مشكلة لا تتردد بالتواصل: 770-245745' },
    { id: 5, name: 'تذكير الدفعة المستحقة', active: true, timing: 'payment_due', type: 'payment', message: 'تذكير: دفعتك الشهرية ({المبلغ} ر.ي) مستحقة في {التاريخ}. للاستفسار: 770-245745' },
    { id: 6, name: 'عيد ميلاد المريض', active: false, timing: 'birthday', type: 'birthday', message: '🎂 عيد ميلاد سعيد {اسم_المريض}! يتمنى لك فريق مركز د. عقلان صحةً وسعادة دائمة.' },
  ]);

  const [editId, setEditId] = React.useState(null);
  const [editMsg, setEditMsg] = React.useState('');
  const [showLog, setShowLog] = React.useState(false);

  const LOG = [
    { id: 1, patient: 'أحمد الشيباني', phone: '770-123456', message: 'تذكير الموعد — 48 ساعة', status: 'delivered', time: '28 أبريل 10:00 ص' },
    { id: 2, patient: 'نورة القحطاني', phone: '711-345678', message: 'تذكير الموعد — 2 ساعة', status: 'delivered', time: '28 أبريل 08:00 ص' },
    { id: 3, patient: 'فاطمة المقطري', phone: '711-654321', message: 'تأكيد الحجز', status: 'delivered', time: '27 أبريل 03:30 م' },
    { id: 4, patient: 'يوسف الزبيدي', phone: '770-901234', message: 'تذكير الدفعة', status: 'failed', time: '27 أبريل 09:00 ص' },
    { id: 5, patient: 'ريم المحاوي', phone: '711-567890', message: 'تذكير الموعد — 48 ساعة', status: 'delivered', time: '26 أبريل 10:00 ص' },
  ];

  const typeColors = { appointment: '#3d7ab5', confirmation: '#22c55e', followup: '#f5922e', payment: '#a855f7', birthday: '#f59e0b' };
  const typeLabels = { appointment: 'موعد', confirmation: 'تأكيد', followup: 'متابعة', payment: 'دفعة', birthday: 'عيد ميلاد' };

  const toggle = (id) => setTemplates(prev => prev.map(t => t.id === id ? {...t, active: !t.active} : t));

  const stats = {
    sent: 847, delivered: 819, failed: 28, deliveryRate: 96.7,
  };

  return (
    <div>
      {/* Stats */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4,1fr)', gap: 14, marginBottom: 20 }}>
        {[
          { label: 'إجمالي المُرسَل', val: stats.sent, icon: 'bell', color: '#3d7ab5', sub: 'هذا الشهر' },
          { label: 'تم التوصيل', val: stats.delivered, icon: 'check', color: '#22c55e', sub: `${stats.deliveryRate}% معدل التوصيل` },
          { label: 'فشل الإرسال', val: stats.failed, icon: 'x', color: '#ef4444', sub: 'تحتاج مراجعة' },
          { label: 'القوالب النشطة', val: templates.filter(t => t.active).length, icon: 'report', color: '#a855f7', sub: `من ${templates.length} قالب` },
        ].map(s => <StatCard key={s.label} label={s.label} value={s.val} icon={s.icon} color={s.color} sub={s.sub} />)}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 360px', gap: 16 }}>
        {/* Templates */}
        <div>
          <SectionHeader title="قوالب الرسائل التلقائية" action={<Btn variant="primary" size="sm" icon="plus">قالب جديد</Btn>} />
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            {templates.map(t => (
              <Card key={t.id}>
                <div style={{ display: 'flex', gap: 14, alignItems: 'flex-start' }}>
                  <div style={{ flex: 1 }}>
                    <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8 }}>
                      <span style={{ fontWeight: 700, fontSize: 14, color: '#0d2137' }}>{t.name}</span>
                      <Badge color={typeColors[t.type]}>{typeLabels[t.type]}</Badge>
                      <Badge color={t.timing === 'immediate' ? '#22c55e' : '#64748b'}>
                        {t.timing === '48h' ? 'قبل 48 ساعة' : t.timing === '2h' ? 'قبل ساعتين' : t.timing === 'immediate' ? 'فوري' : t.timing === '24h_after' ? 'بعد 24 ساعة' : t.timing === 'payment_due' ? 'استحقاق الدفعة' : 'عيد الميلاد'}
                      </Badge>
                    </div>
                    {editId === t.id ? (
                      <div>
                        <textarea value={editMsg} onChange={e => setEditMsg(e.target.value)} style={{ width: '100%', padding: '8px 12px', borderRadius: 8, border: '1.5px solid #3d7ab5', fontFamily: 'Tajawal', fontSize: 13, resize: 'none', outline: 'none', direction: 'rtl', marginBottom: 8 }} rows={3} />
                        <div style={{ display: 'flex', gap: 8 }}>
                          <Btn size="sm" variant="primary" onClick={() => { setTemplates(prev => prev.map(tt => tt.id === t.id ? {...tt, message: editMsg} : tt)); setEditId(null); }}>حفظ</Btn>
                          <Btn size="sm" variant="ghost" onClick={() => setEditId(null)}>إلغاء</Btn>
                        </div>
                      </div>
                    ) : (
                      <div style={{ fontSize: 13, color: '#64748b', background: '#f7fafd', borderRadius: 8, padding: '8px 12px', lineHeight: 1.7, direction: 'rtl' }}>
                        {t.message}
                      </div>
                    )}
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 8, alignItems: 'flex-end', flexShrink: 0 }}>
                    {/* Toggle */}
                    <button onClick={() => toggle(t.id)} style={{ width: 48, height: 26, borderRadius: 99, border: 'none', cursor: 'pointer', background: t.active ? '#3d7ab5' : '#e2e8f0', transition: 'background 0.2s', position: 'relative' }}>
                      <div style={{ position: 'absolute', top: 3, left: t.active ? 24 : 3, width: 20, height: 20, borderRadius: 99, background: '#fff', boxShadow: '0 1px 4px rgba(0,0,0,0.2)', transition: 'left 0.2s' }} />
                    </button>
                    <Btn size="sm" variant="ghost" icon="edit" onClick={() => { setEditId(t.id); setEditMsg(t.message); }}>تعديل</Btn>
                  </div>
                </div>
              </Card>
            ))}
          </div>
        </div>

        {/* Log + test */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          {/* Send test */}
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>إرسال رسالة يدوية</div>
            <div style={{ marginBottom: 10 }}>
              <label style={{ fontSize: 12, color: '#64748b', fontWeight: 600, display: 'block', marginBottom: 4 }}>المريض</label>
              <select style={{ width: '100%', padding: '8px 12px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none' }}>
                {MOCK.patients.map(p => <option key={p.id}>{p.name} — {p.phone}</option>)}
              </select>
            </div>
            <div style={{ marginBottom: 10 }}>
              <label style={{ fontSize: 12, color: '#64748b', fontWeight: 600, display: 'block', marginBottom: 4 }}>القالب</label>
              <select style={{ width: '100%', padding: '8px 12px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none' }}>
                {templates.map(t => <option key={t.id}>{t.name}</option>)}
              </select>
            </div>
            <Btn variant="primary" icon="bell" style={{ width: '100%', justifyContent: 'center' }}>إرسال الآن</Btn>
          </Card>

          {/* Log */}
          <Card padding={0}>
            <div style={{ padding: '14px 16px', borderBottom: '1px solid #f1f5f9', fontWeight: 700, fontSize: 14 }}>سجل الإرسال</div>
            {LOG.map((l, i) => (
              <div key={l.id} style={{ padding: '10px 16px', borderBottom: i < LOG.length - 1 ? '1px solid #f8fafc' : 'none', display: 'flex', gap: 10, alignItems: 'center' }}>
                <div style={{ width: 8, height: 8, borderRadius: 99, background: l.status === 'delivered' ? '#22c55e' : '#ef4444', flexShrink: 0, marginTop: 2 }} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 13, fontWeight: 600, color: '#0d2137' }}>{l.patient}</div>
                  <div style={{ fontSize: 11, color: '#94a3b8', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{l.message}</div>
                  <div style={{ fontSize: 10, color: '#cbd5e1' }}>{l.time}</div>
                </div>
                <Badge color={l.status === 'delivered' ? '#22c55e' : '#ef4444'}>{l.status === 'delivered' ? 'وصل' : 'فشل'}</Badge>
              </div>
            ))}
          </Card>
        </div>
      </div>
    </div>
  );
}

// ============================
// Feature 2: Patient Questionnaires
// ============================

function QuestionnairesPage() {
  const [tab, setTab] = React.useState('templates');
  const [viewId, setViewId] = React.useState(null);

  const QUESTIONNAIRES = [
    {
      id: 1, title: 'استمارة المريض الجديد', type: 'new_patient', responses: 23, status: 'active',
      questions: [
        { id: 1, type: 'text', q: 'ما هي شكواك الرئيسية؟', required: true },
        { id: 2, type: 'radio', q: 'هل تعاني من أمراض مزمنة؟', options: ['نعم', 'لا'], required: true },
        { id: 3, type: 'checkbox', q: 'الأدوية التي تتناولها حالياً', options: ['أدوية ضغط', 'أدوية سكري', 'مضادات تخثر', 'مضادات اكتئاب', 'لا يوجد'] },
        { id: 4, type: 'radio', q: 'هل لديك حساسية من أي دواء؟', options: ['نعم', 'لا'], required: true },
        { id: 5, type: 'text', q: 'إذا نعم، اذكر الأدوية التي تسبب لك حساسية' },
        { id: 6, type: 'radio', q: 'هل سبق لك إجراء أي عمليات جراحية؟', options: ['نعم', 'لا'] },
        { id: 7, type: 'radio', q: 'هل أنت حامل حالياً؟ (للسيدات فقط)', options: ['نعم', 'لا', 'لا ينطبق'] },
        { id: 8, type: 'scale', q: 'كيف تقيّم صحة أسنانك الحالية؟', min: 1, max: 10 },
      ]
    },
    {
      id: 2, title: 'استمارة ما قبل التقويم', type: 'ortho_pre', responses: 41, status: 'active',
      questions: [
        { id: 1, type: 'radio', q: 'هل سبق أن أجريت علاج تقويم من قبل؟', options: ['نعم', 'لا'], required: true },
        { id: 2, type: 'checkbox', q: 'ما العادات الفموية التي تمارسها؟', options: ['مص الإبهام', 'التنفس الفموي', 'الطحن الليلي', 'ضغط اللسان', 'لا يوجد'] },
        { id: 3, type: 'radio', q: 'هل لديك مشكلة في مفصل الفك؟', options: ['نعم', 'لا'] },
        { id: 4, type: 'text', q: 'ما هو هدفك من علاج التقويم؟', required: true },
        { id: 5, type: 'radio', q: 'ما نوع التقويم الذي تفضله؟', options: ['براكيت معدني', 'براكيت شفاف', 'Invisalign', 'أي نوع مناسب'] },
        { id: 6, type: 'radio', q: 'هل مستعد للالتزام بتعليمات التقويم؟', options: ['نعم تماماً', 'سأحاول', 'لست متأكداً'] },
      ]
    },
    {
      id: 3, title: 'تقييم رضا المريض', type: 'satisfaction', responses: 87, status: 'active',
      questions: [
        { id: 1, type: 'scale', q: 'كيف تقيّم جودة الخدمة في العيادة؟', min: 1, max: 5 },
        { id: 2, type: 'scale', q: 'كيف تقيّم مهارة الطبيب؟', min: 1, max: 5 },
        { id: 3, type: 'scale', q: 'كيف تقيّم أوقات الانتظار؟', min: 1, max: 5 },
        { id: 4, type: 'scale', q: 'كيف تقيّم نظافة العيادة؟', min: 1, max: 5 },
        { id: 5, type: 'radio', q: 'هل ستوصي بالعيادة لأصدقائك وعائلتك؟', options: ['نعم بالتأكيد', 'ربما', 'لا'] },
        { id: 6, type: 'text', q: 'ما الذي يمكننا تحسينه؟' },
      ]
    },
  ];

  const RESPONSES = [
    { id: 1, patient: 'أحمد الشيباني', qTitle: 'استمارة المريض الجديد', date: '28 أبريل 2026', status: 'completed' },
    { id: 2, patient: 'فاطمة المقطري', qTitle: 'استمارة المريض الجديد', date: '27 أبريل 2026', status: 'completed' },
    { id: 3, patient: 'نورة القحطاني', qTitle: 'استمارة ما قبل التقويم', date: '26 أبريل 2026', status: 'completed' },
    { id: 4, patient: 'ريم المحاوي', qTitle: 'تقييم رضا المريض', date: '25 أبريل 2026', status: 'completed' },
    { id: 5, patient: 'خالد المروني', qTitle: 'استمارة المريض الجديد', date: '24 أبريل 2026', status: 'pending' },
  ];

  const typeColor = { new_patient: '#3d7ab5', ortho_pre: '#a855f7', satisfaction: '#22c55e' };
  const typeLabel = { new_patient: 'مريض جديد', ortho_pre: 'قبل التقويم', satisfaction: 'تقييم' };

  const q = QUESTIONNAIRES.find(q => q.id === viewId);

  if (viewId && q) {
    return (
      <div>
        <button onClick={() => setViewId(null)} style={{ display: 'flex', gap: 6, alignItems: 'center', background: 'none', border: 'none', cursor: 'pointer', color: '#64748b', fontFamily: 'Tajawal', fontSize: 13, marginBottom: 16, fontWeight: 500, padding: 0 }}>
          ← العودة
        </button>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 320px', gap: 16 }}>
          <Card>
            <div style={{ fontWeight: 800, fontSize: 16, color: '#0d2137', marginBottom: 4 }}>{q.title}</div>
            <Badge color={typeColor[q.type]}>{typeLabel[q.type]}</Badge>
            <div style={{ marginTop: 20, display: 'flex', flexDirection: 'column', gap: 16 }}>
              {q.questions.map((qs, i) => (
                <div key={qs.id} style={{ padding: 16, background: '#f7fafd', borderRadius: 10, border: '1px solid #e8f0f9' }}>
                  <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start', marginBottom: 10 }}>
                    <div style={{ width: 24, height: 24, borderRadius: 99, background: '#3d7ab5', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11, fontWeight: 700, color: '#fff', flexShrink: 0 }}>{i + 1}</div>
                    <div style={{ flex: 1, fontSize: 14, fontWeight: 600, color: '#0d2137' }}>{qs.q} {qs.required && <span style={{ color: '#ef4444' }}>*</span>}</div>
                    <Badge color={qs.type === 'text' ? '#64748b' : qs.type === 'radio' ? '#3d7ab5' : qs.type === 'checkbox' ? '#f5922e' : '#22c55e'}>
                      {qs.type === 'text' ? 'نص' : qs.type === 'radio' ? 'اختيار واحد' : qs.type === 'checkbox' ? 'اختيار متعدد' : 'تقييم'}
                    </Badge>
                  </div>
                  {qs.type === 'text' && <textarea placeholder="إجابة نصية..." rows={2} style={{ width: '100%', padding: '8px 10px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 12, resize: 'none', outline: 'none' }} />}
                  {(qs.type === 'radio' || qs.type === 'checkbox') && (
                    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                      {qs.options?.map(o => (
                        <label key={o} style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 13, cursor: 'pointer', color: '#64748b' }}>
                          <input type={qs.type === 'radio' ? 'radio' : 'checkbox'} name={`q${qs.id}`} style={{ cursor: 'pointer' }} />
                          {o}
                        </label>
                      ))}
                    </div>
                  )}
                  {qs.type === 'scale' && (
                    <div>
                      <input type="range" min={qs.min} max={qs.max} defaultValue={Math.ceil((qs.min + qs.max) / 2)} style={{ width: '100%', accentColor: '#3d7ab5' }} />
                      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, color: '#94a3b8' }}>
                        <span>{qs.min} — ضعيف</span><span>{qs.max} — ممتاز</span>
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
            <div style={{ marginTop: 20, display: 'flex', gap: 10 }}>
              <Btn variant="primary" icon="check">حفظ الاستمارة</Btn>
              <Btn variant="outline" icon="report">تصدير PDF</Btn>
              <Btn variant="ghost" icon="referral">إرسال للمريض</Btn>
            </div>
          </Card>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <Card>
              <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 12 }}>إحصائيات الاستمارة</div>
              {[['عدد الإجابات', q.responses], ['الحالة', 'نشطة'], ['نوع الاستمارة', typeLabel[q.type]], ['عدد الأسئلة', q.questions.length], ['الأسئلة الإلزامية', q.questions.filter(x => x.required).length]].map(([k, v]) => (
                <div key={k} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                  <span style={{ color: '#64748b' }}>{k}</span>
                  <span style={{ fontWeight: 700, color: '#0d2137' }}>{v}</span>
                </div>
              ))}
            </Card>
            <Card>
              <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 12 }}>إرسال للمريض</div>
              <select style={{ width: '100%', padding: '8px 10px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none', marginBottom: 10 }}>
                {MOCK.patients.map(p => <option key={p.id}>{p.name}</option>)}
              </select>
              <Btn variant="primary" style={{ width: '100%', justifyContent: 'center' }}>إرسال عبر SMS</Btn>
            </Card>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div>
      <Tabs tabs={[{id:'templates',label:'الاستمارات'},{id:'responses',label:'الإجابات'}]} active={tab} onChange={setTab} />

      {tab === 'templates' && (
        <div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 14 }}>
            <Btn variant="primary" icon="plus">استمارة جديدة</Btn>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 14 }}>
            {QUESTIONNAIRES.map(q => (
              <Card key={q.id} onClick={() => setViewId(q.id)} style={{ cursor: 'pointer' }}>
                <div style={{ display: 'flex', gap: 10, alignItems: 'center', marginBottom: 12 }}>
                  <div style={{ width: 40, height: 40, borderRadius: 12, background: typeColor[q.type] + '18', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 20 }}>
                    {q.type === 'new_patient' ? '📋' : q.type === 'ortho_pre' ? '🦷' : '⭐'}
                  </div>
                  <div>
                    <div style={{ fontWeight: 700, fontSize: 14, color: '#0d2137' }}>{q.title}</div>
                    <Badge color={typeColor[q.type]}>{typeLabel[q.type]}</Badge>
                  </div>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13 }}>
                  <span style={{ color: '#64748b' }}>{q.questions.length} سؤال</span>
                  <span style={{ fontWeight: 700, color: '#3d7ab5' }}>{q.responses} إجابة</span>
                </div>
                <div style={{ marginTop: 10, display: 'flex', gap: 6 }}>
                  <Btn variant="outline" size="sm" icon="eye">عرض</Btn>
                  <Btn variant="ghost" size="sm">إرسال</Btn>
                </div>
              </Card>
            ))}
          </div>
        </div>
      )}

      {tab === 'responses' && (
        <Card padding={0}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr style={{ background: '#f7fafd', borderBottom: '1px solid #e8f0f9' }}>
                {['المريض','الاستمارة','التاريخ','الحالة',''].map(h => (
                  <th key={h} style={{ padding: '10px 16px', textAlign: 'right', fontWeight: 700, color: '#64748b', fontSize: 12 }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {RESPONSES.map(r => (
                <tr key={r.id} style={{ borderBottom: '1px solid #f8fafc' }}
                  onMouseEnter={e => e.currentTarget.style.background = '#f7fafd'}
                  onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                >
                  <td style={{ padding: '10px 16px', fontWeight: 600, color: '#0d2137' }}>{r.patient}</td>
                  <td style={{ padding: '10px 16px', color: '#64748b' }}>{r.qTitle}</td>
                  <td style={{ padding: '10px 16px', color: '#94a3b8' }}>{r.date}</td>
                  <td style={{ padding: '10px 16px' }}><Badge color={r.status === 'completed' ? '#22c55e' : '#f5922e'}>{r.status === 'completed' ? 'مكتملة' : 'بانتظار الإجابة'}</Badge></td>
                  <td style={{ padding: '10px 16px' }}><Btn variant="ghost" size="sm" icon="eye">عرض</Btn></td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  );
}

// ============================
// Feature 3: Recall System
// ============================

function RecallPage() {
  const [filter, setFilter] = React.useState('all');
  const [selected, setSelected] = React.useState([]);

  const RECALL_PATIENTS = [
    { id: 'P007', name: 'عبدالرحمن محمود السعدي', lastVisit: '5 أغسطس 2024', daysSince: 267, phone: '770-234567', doctor: 'د. هشام القدسي', type: 'general', risk: 'high' },
    { id: 'R001', name: 'سلمى عبدالعزيز الغامدي', lastVisit: '10 يوليو 2024', daysSince: 293, phone: '711-112233', doctor: 'د. عائشة غازي', type: 'general', risk: 'high' },
    { id: 'R002', name: 'محمد حمود الوادعي', lastVisit: '1 سبتمبر 2024', daysSince: 240, phone: '770-445566', doctor: 'د. إيمان الكامل', type: 'general', risk: 'medium' },
    { id: 'R003', name: 'هند سالم العمري', lastVisit: '20 أكتوبر 2024', daysSince: 191, phone: '711-778899', doctor: 'د. عائشة غازي', type: 'ortho', risk: 'medium' },
    { id: 'R004', name: 'أيمن علي الشرعبي', lastVisit: '5 نوفمبر 2024', daysSince: 175, phone: '770-001122', doctor: 'د. عقلان الكامل', type: 'ortho', risk: 'medium' },
    { id: 'R005', name: 'نجوى خالد المروي', lastVisit: '1 ديسمبر 2024', daysSince: 149, phone: '711-334455', doctor: 'د. هشام القدسي', type: 'general', risk: 'low' },
    { id: 'R006', name: 'وليد أحمد الأغبري', lastVisit: '15 يناير 2026', daysSince: 104, phone: '770-667788', doctor: 'د. خلدون البريهي', type: 'surgery', risk: 'low' },
  ];

  const filtered = filter === 'all' ? RECALL_PATIENTS : RECALL_PATIENTS.filter(p => p.risk === filter);
  const riskColor = { high: '#ef4444', medium: '#f5922e', low: '#f59e0b' };
  const riskLabel = { high: 'خطر عالٍ', medium: 'متوسط', low: 'منخفض' };

  const toggleSelect = (id) => setSelected(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]);
  const selectAll = () => setSelected(filtered.map(p => p.id));

  return (
    <div>
      {/* Stats */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4,1fr)', gap: 14, marginBottom: 20 }}>
        <StatCard label="مرضى يحتاجون استدعاء" value={RECALL_PATIENTS.length} icon="users" color="#f5922e" sub="لم يزوروا منذ 3+ أشهر" />
        <StatCard label="خطر عالٍ (+6 أشهر)" value={RECALL_PATIENTS.filter(p => p.risk === 'high').length} icon="bell" color="#ef4444" sub="غائبون أكثر من 180 يوم" />
        <StatCard label="متابعة تقويم مطلوبة" value={RECALL_PATIENTS.filter(p => p.type === 'ortho').length} icon="tooth" color="#3d7ab5" sub="مرضى تقويم غائبون" />
        <StatCard label="تم الاتصال هذا الأسبوع" value={3} icon="check" color="#22c55e" sub="من أصل {RECALL_PATIENTS.length}" />
      </div>

      {/* Filter + actions */}
      <div style={{ display: 'flex', gap: 10, marginBottom: 14, alignItems: 'center' }}>
        <div style={{ display: 'flex', gap: 6 }}>
          {[['all','الكل'],['high','خطر عالٍ'],['medium','متوسط'],['low','منخفض']].map(([val,lbl]) => (
            <button key={val} onClick={() => setFilter(val)} style={{ padding: '6px 14px', borderRadius: 8, border: `1.5px solid ${filter===val ? riskColor[val]||'#3d7ab5' : '#dce8f5'}`, background: filter===val ? (riskColor[val]||'#3d7ab5') : '#fff', color: filter===val ? '#fff' : '#64748b', fontFamily: 'Tajawal', fontWeight: 600, fontSize: 12, cursor: 'pointer' }}>
              {lbl}
            </button>
          ))}
        </div>
        {selected.length > 0 && (
          <div style={{ display: 'flex', gap: 8, marginRight: 'auto' }}>
            <span style={{ fontSize: 13, color: '#64748b', alignSelf: 'center' }}>محدد: {selected.length}</span>
            <Btn variant="primary" size="sm" icon="bell">إرسال SMS للمحدودين</Btn>
            <Btn variant="outline" size="sm">إلغاء التحديد</Btn>
          </div>
        )}
        {selected.length === 0 && (
          <div style={{ marginRight: 'auto', display: 'flex', gap: 8 }}>
            <Btn variant="ghost" size="sm" onClick={selectAll}>تحديد الكل</Btn>
            <Btn variant="primary" size="sm" icon="bell">إرسال SMS للجميع</Btn>
          </div>
        )}
      </div>

      {/* Table */}
      <Card padding={0}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
          <thead>
            <tr style={{ background: '#f7fafd', borderBottom: '2px solid #e8f0f9' }}>
              <th style={{ padding: '10px 16px', width: 40 }}><input type="checkbox" onChange={e => e.target.checked ? selectAll() : setSelected([])} /></th>
              {['المريض','آخر زيارة','الغياب','الطبيب','التخصص','مستوى الخطر',''].map(h => (
                <th key={h} style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 700, color: '#64748b', fontSize: 12 }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {filtered.map(p => (
              <tr key={p.id} style={{ borderBottom: '1px solid #f8fafc', background: selected.includes(p.id) ? '#f0f5fb' : 'transparent', transition: 'background 0.1s' }}
                onMouseEnter={e => { if (!selected.includes(p.id)) e.currentTarget.style.background = '#f7fafd'; }}
                onMouseLeave={e => { if (!selected.includes(p.id)) e.currentTarget.style.background = 'transparent'; }}
              >
                <td style={{ padding: '10px 16px' }}><input type="checkbox" checked={selected.includes(p.id)} onChange={() => toggleSelect(p.id)} /></td>
                <td style={{ padding: '10px 12px' }}>
                  <div style={{ fontWeight: 700, color: '#0d2137' }}>{p.name}</div>
                  <div style={{ fontSize: 11, color: '#94a3b8' }}>{p.phone}</div>
                </td>
                <td style={{ padding: '10px 12px', color: '#64748b' }}>{p.lastVisit}</td>
                <td style={{ padding: '10px 12px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <div style={{ width: 8, height: 8, borderRadius: 99, background: riskColor[p.risk], flexShrink: 0 }} />
                    <span style={{ fontWeight: 700, color: riskColor[p.risk] }}>{p.daysSince} يوم</span>
                  </div>
                </td>
                <td style={{ padding: '10px 12px', fontSize: 12, color: '#64748b' }}>{p.doctor}</td>
                <td style={{ padding: '10px 12px' }}><Badge color={p.type === 'ortho' ? '#3d7ab5' : p.type === 'surgery' ? '#ef4444' : '#22c55e'}>{p.type === 'ortho' ? 'تقويم' : p.type === 'surgery' ? 'جراحة' : 'عام'}</Badge></td>
                <td style={{ padding: '10px 12px' }}><Badge color={riskColor[p.risk]}>{riskLabel[p.risk]}</Badge></td>
                <td style={{ padding: '10px 12px' }}>
                  <div style={{ display: 'flex', gap: 6 }}>
                    <Btn variant="primary" size="sm" icon="bell">SMS</Btn>
                    <Btn variant="ghost" size="sm" icon="calendar">موعد</Btn>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <div style={{ padding: '12px 20px', borderTop: '1px solid #f1f5f9', fontSize: 12, color: '#94a3b8', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span>{filtered.length} مريض يحتاجون متابعة</span>
          <div style={{ display: 'flex', gap: 6 }}>
            <button style={{ padding: '5px 10px', borderRadius: 6, border: '1px solid #dce8f5', background: '#fff', cursor: 'pointer', fontFamily: 'Tajawal', fontSize: 12 }}>تصدير Excel</button>
          </div>
        </div>
      </Card>
    </div>
  );
}

// ============================
// Feature 4: VTO — Visual Treatment Outcome
// ============================

function VTOPage() {
  const [skeletal, setSkeletal] = React.useState(6);   // ANB
  const [overjet, setOverjet] = React.useState(8.5);
  const [upperProtrusion, setUpperProtrusion] = React.useState(6);
  const [lowerRetrusion, setLowerRetrusion] = React.useState(0);
  const [vertPattern, setVertPattern] = React.useState(32); // SN-MP
  const [showAfter, setShowAfter] = React.useState(true);
  const [patient, setPatient] = React.useState('P001');

  // Calculate predicted outcome
  const targetOverjet = 2.5;
  const targetANB = skeletal > 4 ? skeletal - 3 : skeletal;
  const uprightingU1 = upperProtrusion > 0 ? upperProtrusion * 1.5 : 0;

  // SVG profile drawing
  function ProfileSVG({ before }) {
    const cx = 160, cy = 100;
    // Adjustments for after state
    const upperLipAdj = before ? 0 : -(overjet - targetOverjet) * 1.8;
    const lowerLipAdj = before ? 0 : lowerRetrusion * 0.5;
    const chinAdj = before ? 0 : -skeletal * 0.3;

    // Profile path points
    const points = {
      forehead:  [cx - 30 + (before ? 0 : -2), cy - 90],
      nasion:    [cx - 20, cy - 60],
      nasal:     [cx - 50, cy - 30],
      nasal2:    [cx - 55, cy - 15],
      subnasal:  [cx - 30, cy],
      upperLip:  [cx - 25 + upperLipAdj, cy + 12],
      lowerLip:  [cx - 20 + lowerLipAdj + (before ? overjet*1.5 : targetOverjet*1.5), cy + 24],
      chin:      [cx - 10 + chinAdj, cy + 42],
      gnathion:  [cx - 5, cy + 52],
    };

    const color = before ? '#3d7ab5' : '#22c55e';

    const pathD = `M${points.forehead.join(',')} 
      Q${cx-40},${cy-75} ${points.nasion.join(',')}
      Q${cx-35},${cy-45} ${points.nasal.join(',')}
      L${points.nasal2.join(',')}
      Q${cx-40},${cy-5} ${points.subnasal.join(',')}
      Q${cx-28},${cy+6} ${points.upperLip.join(',')}
      Q${cx-22},${cy+18} ${points.lowerLip.join(',')}
      Q${cx-15},${cy+34} ${points.chin.join(',')}
      L${points.gnathion.join(',')}`;

    return (
      <g>
        <path d={pathD} fill="none" stroke={color} strokeWidth={before ? 2.5 : 2} strokeDasharray={before && showAfter ? '6,4' : 'none'} opacity={before && showAfter ? 0.5 : 1} />

        {/* Key landmarks */}
        {[
          { pt: points.nasion, label: 'N' },
          { pt: points.subnasal, label: 'Sn' },
          { pt: points.upperLip, label: 'Ls' },
          { pt: points.lowerLip, label: 'Li' },
          { pt: points.chin, label: 'Pg' },
        ].map(({ pt, label }) => (
          <g key={label}>
            <circle cx={pt[0]} cy={pt[1]} r={3} fill={color} opacity={before && showAfter ? 0.5 : 1} />
            {!before && <text x={pt[0] + 8} y={pt[1] + 4} fontSize="9" fill={color} fontFamily="monospace">{label}</text>}
          </g>
        ))}

        {/* E-line */}
        {!before && (
          <line x1={points.nasal[0]} y1={points.nasal[1]} x2={points.chin[0]} y2={points.chin[1]}
            stroke="#f59e0b" strokeWidth="1" strokeDasharray="3,3" opacity="0.7" />
        )}
      </g>
    );
  }

  const Slider = ({ label, value, min, max, step, unit, onChange, color = '#3d7ab5' }) => (
    <div style={{ marginBottom: 14 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 5, fontSize: 12 }}>
        <span style={{ color: '#64748b', fontWeight: 600 }}>{label}</span>
        <span style={{ fontWeight: 800, color }}>{value}{unit}</span>
      </div>
      <input type="range" min={min} max={max} step={step} value={value} onChange={e => onChange(+e.target.value)}
        style={{ width: '100%', accentColor: color, height: 4 }} />
      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginTop: 2 }}>
        <span>{min}{unit}</span><span>{max}{unit}</span>
      </div>
    </div>
  );

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 320px', gap: 16 }}>
        {/* VTO View */}
        <Card>
          <div style={{ display: 'flex', gap: 14, alignItems: 'center', marginBottom: 16 }}>
            <div style={{ fontWeight: 800, fontSize: 15, color: '#0d2137', flex: 1 }}>محاكاة نتيجة العلاج — VTO</div>
            <select value={patient} onChange={e => setPatient(e.target.value)} style={{ padding: '6px 10px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 12, outline: 'none' }}>
              {MOCK.patients.filter(p => p.type === 'ortho').map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
            <div style={{ display: 'flex', gap: 6 }}>
              <button onClick={() => setShowAfter(!showAfter)} style={{ padding: '6px 12px', borderRadius: 8, border: `1.5px solid ${showAfter ? '#22c55e' : '#dce8f5'}`, background: showAfter ? '#22c55e18' : '#fff', fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600, color: showAfter ? '#22c55e' : '#64748b', cursor: 'pointer' }}>
                {showAfter ? '✅ إظهار المقارنة' : 'إخفاء المقارنة'}
              </button>
            </div>
          </div>

          {/* SVG Profile */}
          <div style={{ display: 'flex', gap: 20, alignItems: 'flex-start' }}>
            <div style={{ flex: 1 }}>
              <div style={{ textAlign: 'center', fontSize: 12, color: '#94a3b8', marginBottom: 8 }}>الملف الجانبي — الوجه</div>
              <svg width="320" height="240" viewBox="0 0 320 240" style={{ display: 'block', margin: '0 auto', background: '#f7fafd', borderRadius: 12, border: '1px solid #e8f0f9' }}>
                {/* Grid */}
                {[80,100,120,140,160,180].map(x => <line key={x} x1={x} y1={10} x2={x} y2={230} stroke="#e8f0f9" strokeWidth={1} />)}
                {[50,100,150,200].map(y => <line key={y} x1={60} y1={y} x2={300} y2={y} stroke="#e8f0f9" strokeWidth={1} />)}

                {/* Skull outline */}
                <ellipse cx={175} cy={60} rx={50} ry={55} fill="none" stroke="#e8f0f9" strokeWidth={1.5} />

                {/* Profiles */}
                {showAfter && <ProfileSVG before={false} />}
                <ProfileSVG before={true} />

                {/* Legend */}
                <rect x={180} y={15} width={12} height={3} rx={1} fill="#3d7ab5" opacity="0.5" />
                <text x={196} y={20} fontSize="9" fill="#3d7ab5" fontFamily="Tajawal">قبل العلاج</text>
                {showAfter && <>
                  <rect x={180} y={28} width={12} height={3} rx={1} fill="#22c55e" />
                  <text x={196} y={33} fontSize="9" fill="#22c55e" fontFamily="Tajawal">بعد العلاج (متوقع)</text>
                </>}

                {/* Overjet arrow */}
                <line x1={105} y1={112} x2={105 + overjet * 4} y2={112} stroke="#f5922e" strokeWidth={2} markerEnd="url(#arrow)" />
                <text x={108} y={107} fontSize="9" fill="#f5922e" fontFamily="monospace">OJ: {overjet}mm</text>
                {showAfter && <line x1={105} y1={120} x2={105 + targetOverjet * 4} y2={120} stroke="#22c55e" strokeWidth={2} />}
                {showAfter && <text x={108} y={132} fontSize="9" fill="#22c55e" fontFamily="monospace">→ {targetOverjet}mm</text>}
              </svg>

              {/* Legend */}
              <div style={{ display: 'flex', gap: 16, justifyContent: 'center', marginTop: 10 }}>
                <div style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 12 }}>
                  <div style={{ width: 20, height: 3, background: '#3d7ab5', borderRadius: 99, opacity: 0.5 }} />
                  <span style={{ color: '#3d7ab5' }}>قبل العلاج</span>
                </div>
                {showAfter && <div style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 12 }}>
                  <div style={{ width: 20, height: 3, background: '#22c55e', borderRadius: 99 }} />
                  <span style={{ color: '#22c55e' }}>بعد العلاج</span>
                </div>}
                <div style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 12 }}>
                  <div style={{ width: 20, height: 1, background: '#f59e0b', borderRadius: 99, borderTop: '1px dashed #f59e0b' }} />
                  <span style={{ color: '#f59e0b' }}>خط E</span>
                </div>
              </div>
            </div>

            {/* Measurements comparison */}
            <div style={{ width: 200, flexShrink: 0 }}>
              <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 12, color: '#0d2137' }}>المقارنة القياسية</div>
              {[
                { label: 'ANB', before: skeletal, after: targetANB, unit: '°', norm: '2±2' },
                { label: 'Overjet', before: overjet, after: targetOverjet, unit: 'mm', norm: '2±1' },
                { label: 'SN-MP', before: vertPattern, after: vertPattern, unit: '°', norm: '32±5' },
                { label: 'U1/SN', before: 104 + upperProtrusion, after: 104, unit: '°', norm: '104±5' },
              ].map(m => (
                <div key={m.label} style={{ marginBottom: 12, padding: '8px 10px', background: '#f7fafd', borderRadius: 8 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                    <span style={{ fontSize: 11, fontWeight: 700, color: '#64748b', fontFamily: 'monospace' }}>{m.label}</span>
                    <span style={{ fontSize: 10, color: '#cbd5e1' }}>طبيعي: {m.norm}</span>
                  </div>
                  <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                    <span style={{ fontSize: 13, fontWeight: 800, color: '#3d7ab5' }}>{m.before}{m.unit}</span>
                    <span style={{ color: '#cbd5e1', fontSize: 12 }}>→</span>
                    <span style={{ fontSize: 13, fontWeight: 800, color: '#22c55e' }}>{m.after.toFixed(1)}{m.unit}</span>
                  </div>
                </div>
              ))}
              <Btn variant="primary" size="sm" icon="report" style={{ width: '100%', justifyContent: 'center', marginTop: 4 }}>
                طباعة VTO
              </Btn>
            </div>
          </div>
        </Card>

        {/* Controls */}
        <Card>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 16, color: '#0d2137' }}>معاملات المحاكاة</div>
          <Slider label="ANB — Class الهيكلية" value={skeletal} min={0} max={12} step={0.5} unit="°" onChange={setSkeletal} color="#ef4444" />
          <Slider label="Overjet الحالي" value={overjet} min={1} max={15} step={0.5} unit="mm" onChange={setOverjet} color="#f5922e" />
          <Slider label="بروز U1/SN زائد" value={upperProtrusion} min={0} max={20} step={1} unit="°" onChange={setUpperProtrusion} color="#a855f7" />
          <Slider label="SN-MP — نمط عمودي" value={vertPattern} min={20} max={50} step={1} unit="°" onChange={setVertPattern} color="#3d7ab5" />

          <div style={{ marginTop: 8, padding: 12, background: '#f0f5fb', borderRadius: 10, border: '1px solid #dce8f5' }}>
            <div style={{ fontWeight: 700, fontSize: 12, color: '#3d7ab5', marginBottom: 8 }}>🤖 توصية تلقائية</div>
            <div style={{ fontSize: 12, color: '#64748b', lineHeight: 1.8 }}>
              {skeletal > 4
                ? `ANB = ${skeletal}° — Class II هيكلي. ${overjet > 6 ? 'Overjet زائد يتطلب إجراءات خلع.' : 'العلاج التقويمي وحده كافٍ.'}`
                : `ANB = ${skeletal}° — Class I. العلاج التقويمي بدون خلع محتمل.`
              }
            </div>
          </div>
        </Card>
      </div>
    </div>
  );
}

// ============================
// Feature 5: Referrals System
// ============================

function ReferralsPage() {
  const [tab, setTab] = React.useState('incoming');
  const [showNew, setShowNew] = React.useState(false);

  const REFERRALS = {
    incoming: [
      { id: 1, patient: 'محمد سالم الحمادي', fromDoctor: 'د. إيمان الكامل', fromSpec: 'طب عام', toDoctor: 'د. عقلان الكامل', toSpec: 'تقويم', reason: 'ازدحام أسنان شديد وعلاقة رحوية Class II — الرجاء الفحص والرأي', priority: 'normal', date: '28 أبريل 2026', status: 'pending' },
      { id: 2, patient: 'سارة فاروق الشامي', fromDoctor: 'د. عائشة غازي', fromSpec: 'طب عام', toDoctor: 'د. خلدون البريهي', toSpec: 'جراحة', reason: 'خراج تحت اللساني يحتاج تدخل جراحي عاجل', priority: 'urgent', date: '27 أبريل 2026', status: 'accepted' },
      { id: 3, patient: 'ريم المحاوي', fromDoctor: 'د. عقلان الكامل', fromSpec: 'تقويم', toDoctor: 'د. هشام القدسي', toSpec: 'طب عام', reason: 'التهاب لثوي يؤثر على تقدم التقويم — يحتاج علاج لثة قبل الاستمرار', priority: 'normal', date: '25 أبريل 2026', status: 'completed' },
    ],
    outgoing: [
      { id: 4, patient: 'يوسف حسن الزبيدي', fromDoctor: 'د. عقلان الكامل', fromSpec: 'تقويم', toDoctor: 'د. خلدون البريهي', toSpec: 'جراحة وجه وفكين', reason: 'تحضير جراحي لحالة تقويم جراحية مشتركة — يحتاج VTO مشترك', priority: 'normal', date: '26 أبريل 2026', status: 'pending' },
      { id: 5, patient: 'أحمد الشيباني', fromDoctor: 'د. عقلان الكامل', fromSpec: 'تقويم', toDoctor: 'د. عائشة غازي', toSpec: 'طب عام', reason: 'خلع الضواحك الأولى (14، 24) مطلوب قبل إغلاق الفراغات', priority: 'normal', date: '20 أبريل 2026', status: 'completed' },
    ],
  };

  const prColor = { urgent: '#ef4444', normal: '#3d7ab5', low: '#22c55e' };
  const stColor = { pending: '#f5922e', accepted: '#3d7ab5', completed: '#22c55e', rejected: '#ef4444' };
  const stLabel = { pending: 'انتظار', accepted: 'مقبولة', completed: 'مكتملة', rejected: 'مرفوضة' };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 14 }}>
        <Btn variant="primary" icon="referral" onClick={() => setShowNew(true)}>إحالة جديدة</Btn>
      </div>

      <Tabs tabs={[{id:'incoming',label:`الواردة (${REFERRALS.incoming.length})`},{id:'outgoing',label:`الصادرة (${REFERRALS.outgoing.length})`}]} active={tab} onChange={setTab} />

      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        {REFERRALS[tab].map(r => (
          <Card key={r.id}>
            <div style={{ display: 'flex', gap: 14, alignItems: 'flex-start' }}>
              <div style={{ width: 44, height: 44, borderRadius: 12, background: prColor[r.priority] + '18', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                <Icon name="referral" size={20} stroke={prColor[r.priority]} />
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ display: 'flex', gap: 10, alignItems: 'center', marginBottom: 6, flexWrap: 'wrap' }}>
                  <span style={{ fontWeight: 800, fontSize: 15, color: '#0d2137' }}>{r.patient}</span>
                  {r.priority === 'urgent' && <Badge color="#ef4444">⚡ عاجل</Badge>}
                  <Badge color={stColor[r.status]}>{stLabel[r.status]}</Badge>
                  <span style={{ fontSize: 12, color: '#94a3b8' }}>{r.date}</span>
                </div>
                <div style={{ display: 'flex', gap: 16, marginBottom: 8, fontSize: 13 }}>
                  <div><span style={{ color: '#94a3b8' }}>من: </span><span style={{ fontWeight: 600, color: '#0d2137' }}>{r.fromDoctor}</span><span style={{ color: '#94a3b8' }}> ({r.fromSpec})</span></div>
                  <div style={{ color: '#cbd5e1' }}>←</div>
                  <div><span style={{ color: '#94a3b8' }}>إلى: </span><span style={{ fontWeight: 600, color: '#3d7ab5' }}>{r.toDoctor}</span><span style={{ color: '#94a3b8' }}> ({r.toSpec})</span></div>
                </div>
                <div style={{ padding: '8px 12px', background: '#f7fafd', borderRadius: 8, fontSize: 13, color: '#64748b', lineHeight: 1.7 }}>{r.reason}</div>
              </div>
              <div style={{ display: 'flex', gap: 8, flexShrink: 0, flexDirection: 'column', alignItems: 'flex-end' }}>
                {r.status === 'pending' && tab === 'incoming' && (
                  <>
                    <Btn variant="primary" size="sm" icon="check">قبول</Btn>
                    <Btn variant="danger" size="sm">رفض</Btn>
                  </>
                )}
                <Btn variant="ghost" size="sm" icon="eye">التفاصيل</Btn>
                <Btn variant="outline" size="sm" icon="report">طباعة</Btn>
              </div>
            </div>
          </Card>
        ))}
      </div>

      {/* New Referral Modal */}
      {showNew && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(13,33,55,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
          <div style={{ width: 560, background: '#fff', borderRadius: 20, boxShadow: '0 20px 60px rgba(13,33,55,0.2)', overflow: 'hidden' }}>
            <div style={{ padding: '20px 24px', borderBottom: '1px solid #f1f5f9', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: '#0d2137' }}>
              <span style={{ fontSize: 16, fontWeight: 800, color: '#fff' }}>إحالة مريض جديدة</span>
              <button onClick={() => setShowNew(false)} style={{ background: 'rgba(255,255,255,0.15)', border: 'none', borderRadius: 8, width: 30, height: 30, cursor: 'pointer', color: '#fff', fontSize: 14 }}>✕</button>
            </div>
            <div style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 14 }}>
              {[
                ['المريض', 'select', MOCK.patients.map(p => p.name)],
                ['إلى الطبيب', 'select', MOCK.doctors.map(d => d.name)],
                ['الأولوية', 'select', ['عادي', 'عاجل', 'منخفض']],
              ].map(([label, type, opts]) => (
                <div key={label}>
                  <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#64748b', marginBottom: 5 }}>{label}</label>
                  <select style={{ width: '100%', padding: '9px 12px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none' }}>
                    {opts.map(o => <option key={o}>{o}</option>)}
                  </select>
                </div>
              ))}
              <div>
                <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#64748b', marginBottom: 5 }}>سبب الإحالة *</label>
                <textarea placeholder="اذكر السبب الطبي للإحالة والمعلومات الضرورية..." rows={4} style={{ width: '100%', padding: '9px 12px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none', resize: 'none' }} />
              </div>
              <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', marginTop: 4 }}>
                <Btn variant="ghost" onClick={() => setShowNew(false)}>إلغاء</Btn>
                <Btn variant="primary" icon="check" onClick={() => setShowNew(false)}>إرسال الإحالة</Btn>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

Object.assign(window, { SMSRemindersPage, QuestionnairesPage, RecallPage, VTOPage, ReferralsPage });
