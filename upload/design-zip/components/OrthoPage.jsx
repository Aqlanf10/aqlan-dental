
// ============================
// Ortho Case Page — Full
// ============================

function OrthoPage({ patient, onBack }) {
  const [tab, setTab] = React.useState('overview');
  const c = MOCK.orthoCase;

  const TABS = [
    { id: 'overview', label: 'نظرة عامة' },
    { id: 'exam', label: 'الفحص السريري' },
    { id: 'problems', label: 'قائمة المشاكل' },
    { id: 'plan', label: 'خطة العلاج' },
    { id: 'visits', label: 'الزيارات' },
    { id: 'stages', label: 'المراحل' },
    { id: 'photos', label: 'الصور' },
    { id: 'ceph', label: 'السيفالومتري' },
    { id: 'extraction', label: 'قرار الخلع' },
    { id: 'retention', label: 'Retention' },
  ];

  const paidPct = Math.round((c.paidAmount / c.totalFee) * 100);

  // ── Overview ─────────────────────────────────────────
  function OverviewTab() {
    return (
      <div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4,1fr)', gap: 14, marginBottom: 18 }}>
          {[
            { label: 'نوع الجهاز', val: c.appliance, icon: 'tooth', color: '#3d7ab5' },
            { label: 'تاريخ البدء', val: c.startDate, icon: 'calendar', color: '#f5922e' },
            { label: 'المدة المتوقعة', val: `${c.expectedDuration} شهر`, icon: 'clock', color: '#a855f7' },
            { label: 'المرحلة الحالية', val: `${c.stagePercentage}%`, icon: 'chart', color: '#22c55e' },
          ].map(i => (
            <Card key={i.label}>
              <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                <div style={{ width: 38, height: 38, borderRadius: 10, background: i.color + '18', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                  <Icon name={i.icon} size={18} stroke={i.color} />
                </div>
                <div>
                  <div style={{ fontSize: 11, color: '#94a3b8', fontWeight: 500 }}>{i.label}</div>
                  <div style={{ fontSize: 15, fontWeight: 800, color: '#0d2137' }}>{i.val}</div>
                </div>
              </div>
            </Card>
          ))}
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
          {/* Treatment progress */}
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, color: '#0d2137', marginBottom: 16 }}>تقدم العلاج</div>
            <div style={{ fontSize: 12, color: '#94a3b8', marginBottom: 4 }}>المرحلة: {c.currentStage}</div>
            <div style={{ height: 10, background: '#f1f5f9', borderRadius: 99, overflow: 'hidden', marginBottom: 6 }}>
              <div style={{ width: `${c.stagePercentage}%`, height: '100%', background: 'linear-gradient(90deg, #3d7ab5, #2d5e8e)', borderRadius: 99, transition: 'width 1s' }} />
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#94a3b8', marginBottom: 18 }}>
              <span>{c.stagePercentage}% مكتمل</span><span>المرحلة 1 من 5</span>
            </div>

            <div style={{ display: 'flex', gap: 2 }}>
              {c.stages.map((s, i) => (
                <div key={i} style={{ flex: 1, textAlign: 'center' }}>
                  <div style={{ height: 6, background: s.status === 'active' ? s.color : s.status === 'completed' ? '#22c55e' : '#e8f0f9', borderRadius: 3, margin: '0 1px', position: 'relative', overflow: s.status === 'active' ? 'hidden' : 'visible' }}>
                    {s.status === 'active' && <div style={{ position: 'absolute', top: 0, left: 0, height: '100%', width: `${s.progress}%`, background: s.color }} />}
                  </div>
                  <div style={{ fontSize: 9, color: '#94a3b8', marginTop: 4, lineHeight: 1.2 }}>{s.name.split('و')[0].trim()}</div>
                </div>
              ))}
            </div>
          </Card>

          {/* Financial */}
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, color: '#0d2137', marginBottom: 16 }}>الوضع المالي</div>
            {[
              ['إجمالي العقد', `${c.totalFee.toLocaleString()} ر.ي`, '#0d2137'],
              ['المحصّل', `${c.paidAmount.toLocaleString()} ر.ي`, '#22c55e'],
              ['المتبقي', `${(c.totalFee - c.paidAmount).toLocaleString()} ر.ي`, '#f5922e'],
            ].map(([k, v, col]) => (
              <div key={k} style={{ display: 'flex', justifyContent: 'space-between', padding: '7px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                <span style={{ color: '#64748b' }}>{k}</span>
                <span style={{ fontWeight: 700, color: col }}>{v}</span>
              </div>
            ))}
            <div style={{ marginTop: 14 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#64748b', marginBottom: 4 }}><span>التحصيل</span><span>{paidPct}%</span></div>
              <div style={{ height: 8, background: '#f1f5f9', borderRadius: 99, overflow: 'hidden' }}>
                <div style={{ width: `${paidPct}%`, height: '100%', background: 'linear-gradient(90deg,#22c55e,#16a34a)', borderRadius: 99 }} />
              </div>
            </div>
          </Card>
        </div>

        {/* Last visit summary */}
        <Card>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>آخر زيارة — {c.visits[c.visits.length - 1].date}</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4,1fr)', gap: 12 }}>
            {[
              ['نوع الزيارة', c.visits[c.visits.length - 1].type],
              ['سلك علوي', c.visits[c.visits.length - 1].wireUpper],
              ['سلك سفلي', c.visits[c.visits.length - 1].wireLower],
              ['Overjet', `${c.visits[c.visits.length - 1].overjet} mm`],
            ].map(([k, v]) => (
              <div key={k} style={{ background: '#f7fafd', borderRadius: 8, padding: 12 }}>
                <div style={{ fontSize: 11, color: '#94a3b8', marginBottom: 4 }}>{k}</div>
                <div style={{ fontSize: 14, fontWeight: 700, color: '#0d2137' }}>{v}</div>
              </div>
            ))}
          </div>
          <div style={{ marginTop: 12, padding: 12, background: '#f7fafd', borderRadius: 8, fontSize: 13, color: '#64748b' }}>{c.visits[c.visits.length - 1].notes}</div>
        </Card>
      </div>
    );
  }

  // ── Clinical Exam ─────────────────────────────────────────
  function ExamTab() {
    const ex = c.clinicalExam.extraoral;
    const intra = c.clinicalExam.intraoral;
    const Field = ({ label, val, alert }) => (
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
        <span style={{ color: '#64748b' }}>{label}</span>
        <span style={{ fontWeight: 700, color: alert ? '#f5922e' : '#0d2137' }}>{val}</span>
      </div>
    );

    return (
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
        <Card>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 4, color: '#3d7ab5' }}>الفحص الخارجي — Extraoral</div>
          <div style={{ fontSize: 11, color: '#94a3b8', marginBottom: 14 }}>ملاحظات خارج الفم</div>
          <Field label="تماثل الوجه" val={ex.facialSymmetry} />
          <Field label="الملف الجانبي" val={ex.profile} alert />
          <Field label="تقابل الشفاه" val={ex.lipsCompetence ? 'طبيعي' : 'لا يتقابلان'} alert={!ex.lipsCompetence} />
          <Field label="خط الابتسامة" val={ex.smileLine} />
          <Field label="النسب العمودية" val={ex.verticalProportion} />
          <Field label="الزاوية الأنفية الشفوية" val={ex.nasolabialAngle} />
        </Card>

        <Card>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 4, color: '#f5922e' }}>الفحص الداخلي — Intraoral</div>
          <div style={{ fontSize: 11, color: '#94a3b8', marginBottom: 14 }}>قياسات وعلاقات داخل الفم</div>
          <Field label="العلاقة الرحوية" val={intra.molarRelation} alert />
          <Field label="العلاقة الناشبية" val={intra.canineRelation} alert />
          <Field label="Overjet" val={`${intra.overjet} mm`} alert />
          <Field label="Overbite" val={`${intra.overbite} mm`} alert />
          <Field label="Crossbite" val={intra.crossbite ? 'يوجد' : 'لا يوجد'} />
          <Field label="Open Bite" val={intra.openBite ? 'يوجد' : 'لا يوجد'} />
          <Field label="ازدحام علوي" val={intra.upperCrowding === 'moderate' ? 'متوسط' : intra.upperCrowding === 'mild' ? 'خفيف' : 'شديد'} alert />
          <Field label="ازدحام سفلي" val={intra.lowerCrowding === 'mild' ? 'خفيف' : 'متوسط'} />
          <Field label="Midline علوي" val={intra.midlineUpper} alert />
          <Field label="Midline سفلي" val={intra.midlineLower} />
          <Field label="CO/CR" val={intra.cocrDiscrepancy ? 'Discrepancy موجود' : 'طبيعي'} alert={intra.cocrDiscrepancy} />
          <Field label="TMJ" val={intra.tmjFindings} />
          <Field label="اللثة" val={intra.perio} />
        </Card>
      </div>
    );
  }

  // ── Problem List ─────────────────────────────────────────
  function ProblemsTab() {
    const cats = {
      skeletal: { label: 'هيكلية', color: '#ef4444' },
      dental: { label: 'سنية', color: '#3d7ab5' },
      soft_tissue: { label: 'أنسجة رخوة', color: '#f5922e' },
      functional: { label: 'وظيفية', color: '#a855f7' },
      space: { label: 'فراغات', color: '#22c55e' },
    };
    const sevColor = { mild: '#f59e0b', moderate: '#f5922e', severe: '#ef4444' };
    const sevLabel = { mild: 'خفيفة', moderate: 'متوسطة', severe: 'شديدة' };

    const grouped = {};
    c.problemList.forEach(p => {
      if (!grouped[p.category]) grouped[p.category] = [];
      grouped[p.category].push(p);
    });

    return (
      <div>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <div style={{ fontSize: 13, color: '#64748b' }}>
            إجمالي: <strong style={{ color: '#0d2137' }}>{c.problemList.length} مشكلة</strong>
          </div>
          <Btn variant="primary" size="sm" icon="plus">إضافة مشكلة</Btn>
        </div>

        {Object.entries(grouped).map(([cat, items]) => (
          <Card key={cat} style={{ marginBottom: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12 }}>
              <div style={{ width: 10, height: 10, borderRadius: 99, background: cats[cat]?.color }} />
              <span style={{ fontWeight: 700, fontSize: 14, color: cats[cat]?.color }}>{cats[cat]?.label}</span>
              <Badge color={cats[cat]?.color}>{items.length}</Badge>
            </div>
            {items.map((p, i) => (
              <div key={p.id} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '8px 0', borderBottom: i < items.length - 1 ? '1px solid #f8fafc' : 'none' }}>
                <div style={{ width: 24, height: 24, borderRadius: 6, background: '#f7fafd', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11, fontWeight: 700, color: '#94a3b8', flexShrink: 0 }}>{p.id}</div>
                <div style={{ flex: 1, fontSize: 13, color: '#0d2137', fontWeight: 500 }}>{p.description}</div>
                <Badge color={sevColor[p.severity]}>{sevLabel[p.severity]}</Badge>
              </div>
            ))}
          </Card>
        ))}
      </div>
    );
  }

  // ── Treatment Plan ─────────────────────────────────────────
  function PlanTab() {
    const p = c.treatmentPlan;
    return (
      <div>
        <div style={{ display: 'flex', gap: 10, marginBottom: 16, alignItems: 'center' }}>
          <Badge color="#22c55e">✓ معتمدة</Badge>
          <span style={{ fontSize: 13, color: '#64748b' }}>اعتماد: {p.approvedDate}</span>
          <div style={{ marginRight: 'auto' }}>
            <Btn variant="outline" size="sm" icon="edit">تعديل الخطة</Btn>
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14, color: '#3d7ab5' }}>التفاصيل التقنية</div>
            {[
              ['نوع الجهاز', p.applianceType],
              ['نظام البراكيت', p.bracketSystem],
              ['السلك الأولي', p.initialWire],
              ['خطة الخلع', p.extractionPlan],
              ['خطة الإرساء', p.anchoragePlan],
              ['Elastics', p.useElastics ? 'Class II' : 'لا'],
              ['TADs', p.useTADs ? 'نعم' : 'لا'],
              ['التثبيت', p.retentionPlan],
              ['المدة المتوقعة', `${p.expectedDuration} شهر`],
            ].map(([k, v]) => (
              <div key={k} style={{ display: 'flex', justifyContent: 'space-between', padding: '7px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                <span style={{ color: '#64748b' }}>{k}</span>
                <span style={{ fontWeight: 600, color: '#0d2137', textAlign: 'left', maxWidth: 220 }}>{v}</span>
              </div>
            ))}
          </Card>

          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14, color: '#f5922e' }}>أهداف العلاج</div>
            {p.goals.map((g, i) => (
              <div key={i} style={{ display: 'flex', gap: 10, alignItems: 'flex-start', padding: '8px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                <div style={{ width: 20, height: 20, borderRadius: 99, background: '#3d7ab5', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, marginTop: 1 }}>
                  <Icon name="check" size={11} stroke="#fff" strokeWidth={2.5} />
                </div>
                <span style={{ color: '#0d2137' }}>{g}</span>
              </div>
            ))}
          </Card>
        </div>

        {/* Wire sequence */}
        <Card>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>تسلسل الأسلاك المقترح</div>
          <div style={{ display: 'flex', gap: 0, alignItems: 'center', overflowX: 'auto', padding: '4px 0' }}>
            {[
              { wire: 'NiTi 0.014', stage: 'محاذاة', color: '#a855f7' },
              { wire: 'NiTi 0.016', stage: 'محاذاة', color: '#a855f7' },
              { wire: 'NiTi 0.018', stage: 'تسوية', color: '#3d7ab5' },
              { wire: 'NiTi 0.016×22', stage: 'تسوية', color: '#3d7ab5' },
              { wire: 'SS 0.019×25', stage: 'إغلاق', color: '#f5922e' },
              { wire: 'SS 0.017×25', stage: 'تشطيب', color: '#22c55e' },
            ].map((w, i, arr) => (
              <React.Fragment key={i}>
                <div style={{ textAlign: 'center', padding: '8px 12px', minWidth: 100 }}>
                  <div style={{ height: 28, borderRadius: 6, background: w.color + '18', border: `1.5px solid ${w.color}40`, display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: 4 }}>
                    <span style={{ fontSize: 11, fontWeight: 700, color: w.color, direction: 'ltr' }}>{w.wire}</span>
                  </div>
                  <div style={{ fontSize: 10, color: '#94a3b8' }}>{w.stage}</div>
                </div>
                {i < arr.length - 1 && <div style={{ fontSize: 16, color: '#dce8f5', flexShrink: 0 }}>←</div>}
              </React.Fragment>
            ))}
          </div>
        </Card>
      </div>
    );
  }

  // ── Visits ─────────────────────────────────────────
  function VisitsTab() {
    const [expanded, setExpanded] = React.useState(null);
    return (
      <div>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
          <div style={{ fontSize: 13, color: '#64748b' }}>{c.visits.length} زيارة مسجّلة</div>
          <Btn variant="primary" size="sm" icon="plus">تسجيل زيارة جديدة</Btn>
        </div>

        {/* Overjet/Overbite progress chart */}
        <Card style={{ marginBottom: 16 }}>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>تطور Overjet عبر الزيارات</div>
          <div style={{ display: 'flex', alignItems: 'flex-end', gap: 8, height: 64 }}>
            {c.visits.map((v, i) => {
              const h = Math.round((v.overjet / 10) * 56);
              return (
                <div key={v.id} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
                  <div style={{ fontSize: 10, color: '#3d7ab5', fontWeight: 700 }}>{v.overjet}</div>
                  <div style={{ width: '100%', height: h, background: i === c.visits.length - 1 ? '#3d7ab5' : '#dce8f5', borderRadius: '4px 4px 0 0' }} />
                  <div style={{ fontSize: 9, color: '#94a3b8' }}>#{v.visitNumber}</div>
                </div>
              );
            })}
          </div>
          <div style={{ fontSize: 11, color: '#94a3b8', marginTop: 8, textAlign: 'center' }}>تراجع Overjet من 8.5mm → 6.0mm خلال 7 أشهر</div>
        </Card>

        {c.visits.slice().reverse().map((v, i) => (
          <Card key={v.id} style={{ marginBottom: 10, cursor: 'pointer' }} padding={0}>
            <div onClick={() => setExpanded(expanded === v.id ? null : v.id)} style={{ padding: '14px 18px', display: 'flex', gap: 14, alignItems: 'center' }}>
              <div style={{ width: 36, height: 36, borderRadius: 99, background: '#3d7ab5' + '18', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 14, fontWeight: 800, color: '#3d7ab5', flexShrink: 0 }}>
                {v.visitNumber}
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 3 }}>
                  <span style={{ fontWeight: 700, fontSize: 14, color: '#0d2137' }}>{v.type}</span>
                  <Badge color="#3d7ab5" bg="#f0f5fb">{v.date}</Badge>
                </div>
                <div style={{ fontSize: 12, color: '#94a3b8' }}>
                  علوي: {v.wireUpper} · سفلي: {v.wireLower} · OJ: {v.overjet}mm · OB: {v.overbite}mm
                </div>
              </div>
              <Icon name="chevronDown" size={16} stroke="#94a3b8" style={{ transform: expanded === v.id ? 'rotate(180deg)' : 'none', transition: 'transform 0.2s' }} />
            </div>
            {expanded === v.id && (
              <div style={{ padding: '0 18px 16px', borderTop: '1px solid #f1f5f9' }}>
                <div style={{ marginTop: 14, display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 12 }}>
                  {[
                    ['السلك العلوي', v.wireUpper],
                    ['السلك السفلي', v.wireLower],
                    ['Overjet', `${v.overjet} mm`],
                    ['Overbite', `${v.overbite} mm`],
                    ['الموعد القادم', v.nextDate],
                  ].map(([k, val]) => (
                    <div key={k} style={{ background: '#f7fafd', borderRadius: 8, padding: '8px 12px' }}>
                      <div style={{ fontSize: 11, color: '#94a3b8', marginBottom: 2 }}>{k}</div>
                      <div style={{ fontSize: 13, fontWeight: 700, color: '#0d2137' }}>{val}</div>
                    </div>
                  ))}
                </div>
                <div style={{ background: '#f7fafd', borderRadius: 8, padding: 12, fontSize: 13, color: '#64748b', lineHeight: 1.7 }}>{v.notes}</div>
              </div>
            )}
          </Card>
        ))}
      </div>
    );
  }

  // ── Stages ─────────────────────────────────────────
  function StagesTab() {
    return (
      <div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5,1fr)', gap: 3, marginBottom: 24 }}>
          {c.stages.map((s, i) => (
            <div key={i} style={{ height: 6, background: s.status === 'active' ? 'transparent' : s.status === 'completed' ? '#22c55e' : '#e8f0f9', borderRadius: 3, position: 'relative', overflow: s.status === 'active' ? 'hidden' : 'visible' }}>
              {s.status === 'active' && <>
                <div style={{ position: 'absolute', inset: 0, background: '#e8f0f9', borderRadius: 3 }} />
                <div style={{ position: 'absolute', top: 0, right: 0, height: '100%', width: `${s.progress}%`, background: s.color, borderRadius: 3 }} />
              </>}
            </div>
          ))}
        </div>

        {c.stages.map((s, i) => (
          <Card key={i} style={{ marginBottom: 12 }}>
            <div style={{ display: 'flex', gap: 16, alignItems: 'center' }}>
              <div style={{ width: 44, height: 44, borderRadius: 12, background: s.status === 'active' ? s.color : s.status === 'completed' ? '#22c55e18' : '#f1f5f9', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                {s.status === 'completed'
                  ? <Icon name="check" size={20} stroke="#22c55e" strokeWidth={2.5} />
                  : <span style={{ fontSize: 16, fontWeight: 800, color: s.status === 'active' ? '#fff' : '#cbd5e1' }}>{s.order}</span>
                }
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
                  <span style={{ fontWeight: 700, fontSize: 15, color: '#0d2137' }}>{s.name}</span>
                  <Badge
                    color={s.status === 'active' ? s.color : s.status === 'completed' ? '#22c55e' : '#94a3b8'}
                  >
                    {s.status === 'active' ? 'جارٍ' : s.status === 'completed' ? 'مكتمل' : 'لم يبدأ'}
                  </Badge>
                </div>
                {s.status === 'active' && (
                  <div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#94a3b8', marginBottom: 4 }}>
                      <span>التقدم</span><span>{s.progress}%</span>
                    </div>
                    <div style={{ height: 6, background: '#f1f5f9', borderRadius: 99, overflow: 'hidden' }}>
                      <div style={{ width: `${s.progress}%`, height: '100%', background: s.color, borderRadius: 99 }} />
                    </div>
                  </div>
                )}
                <div style={{ fontSize: 12, color: '#94a3b8', marginTop: s.status === 'active' ? 8 : 0 }}>
                  المدة المستهدفة: {s.targetDuration} أشهر
                  {s.startDate && ` · بدأت: ${s.startDate}`}
                </div>
              </div>
            </div>
          </Card>
        ))}
      </div>
    );
  }

  // ── Photos ─────────────────────────────────────────
  function PhotosTab() {
    const [photoTab, setPhotoTab] = React.useState('initial');
    const extraoral = ['صورة أمامية مريح', 'صورة أمامية ابتسامة', 'صورة جانبية يمين'];
    const intraoral = ['صورة أمامية', 'صورة جانبية يمين', 'صورة جانبية يسار', 'صورة إطباقية علوي', 'صورة إطباقية سفلي', 'الابتسامة الكاملة'];

    return (
      <div>
        <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
          {[['initial', 'T0 — بداية'], ['progress', 'T1 — أثناء'], ['final', 'T2 — نهاية']].map(([val, lbl]) => (
            <button key={val} onClick={() => setPhotoTab(val)} style={{ padding: '6px 14px', borderRadius: 8, border: `1.5px solid ${photoTab === val ? '#3d7ab5' : '#dce8f5'}`, background: photoTab === val ? '#3d7ab5' : '#fff', color: photoTab === val ? '#fff' : '#64748b', fontFamily: 'Tajawal', fontSize: 13, fontWeight: 600, cursor: 'pointer' }}>
              {lbl}
            </button>
          ))}
          <div style={{ marginRight: 'auto' }}><Btn variant="primary" size="sm" icon="plus">رفع صور</Btn></div>
        </div>

        {/* Extraoral */}
        <div style={{ fontWeight: 700, fontSize: 13, color: '#64748b', marginBottom: 10 }}>الصور الخارجية — Extraoral</div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 12, marginBottom: 20 }}>
          {extraoral.map((label, i) => (
            <div key={i} style={{ borderRadius: 10, overflow: 'hidden', border: '2px solid #e8f0f9' }}>
              <div style={{ height: 140, background: `repeating-linear-gradient(45deg, #f0f5fb 0px, #f0f5fb 8px, #e8effa 8px, #e8effa 16px)`, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 8 }}>
                <Icon name="user" size={28} stroke="#cbd5e1" />
                <span style={{ fontSize: 11, color: '#94a3b8', fontFamily: 'monospace', textAlign: 'center', padding: '0 8px' }}>{label}</span>
              </div>
              <div style={{ padding: '6px 10px', background: '#fff', fontSize: 11, color: '#64748b', fontWeight: 600 }}>{label}</div>
            </div>
          ))}
        </div>

        {/* Intraoral */}
        <div style={{ fontWeight: 700, fontSize: 13, color: '#64748b', marginBottom: 10 }}>الصور الداخلية — Intraoral</div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 12 }}>
          {intraoral.map((label, i) => (
            <div key={i} style={{ borderRadius: 10, overflow: 'hidden', border: '2px solid #e8f0f9' }}>
              <div style={{ height: 120, background: `repeating-linear-gradient(135deg, #f0f5fb 0px, #f0f5fb 8px, #e8effa 8px, #e8effa 16px)`, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 6 }}>
                <Icon name="tooth" size={24} stroke="#cbd5e1" />
                <span style={{ fontSize: 10, color: '#94a3b8', fontFamily: 'monospace', textAlign: 'center', padding: '0 8px' }}>{label}</span>
              </div>
              <div style={{ padding: '6px 10px', background: '#fff', fontSize: 11, color: '#64748b', fontWeight: 600 }}>{label}</div>
            </div>
          ))}
        </div>
      </div>
    );
  }

  // ── Ceph ─────────────────────────────────────────
  function CephTab() {
    const [analysis, setAnalysis] = React.useState('steiner');
    const measurements = c.cephMeasurements;

    const getDeviation = (m) => {
      const diff = m.value - m.norm;
      const ratio = Math.abs(diff) / m.sd;
      if (ratio <= 1) return 'normal';
      if (ratio <= 2) return 'mild';
      return 'severe';
    };

    const devColor = { normal: '#22c55e', mild: '#f59e0b', severe: '#ef4444' };
    const devLabel = { normal: 'طبيعي', mild: 'خفيف', severe: 'شديد' };

    // Radar chart (polygon)
    function RadarChart() {
      const items = measurements.filter(m => m.category === 'skeletal').slice(0, 6);
      const cx = 120, cy = 120, r = 90;
      const n = items.length;
      const pts = (scale) => items.map((_, i) => {
        const angle = (i / n) * 2 * Math.PI - Math.PI / 2;
        return [cx + Math.cos(angle) * r * scale, cy + Math.sin(angle) * r * scale];
      });

      const grid = [0.25, 0.5, 0.75, 1].map(s => pts(s).map(p => p.join(',')).join(' '));

      const scores = items.map(m => {
        const dev = Math.abs(m.value - m.norm) / m.sd;
        return Math.max(0.15, Math.min(1, 1 - dev * 0.25));
      });

      const dataPoints = items.map((_, i) => {
        const angle = (i / n) * 2 * Math.PI - Math.PI / 2;
        const scale = scores[i];
        return [cx + Math.cos(angle) * r * scale, cy + Math.sin(angle) * r * scale];
      });

      const labelPts = items.map((m, i) => {
        const angle = (i / n) * 2 * Math.PI - Math.PI / 2;
        return { x: cx + Math.cos(angle) * (r + 22), y: cy + Math.sin(angle) * (r + 22), label: m.name };
      });

      return (
        <svg width={240} height={240} viewBox="0 0 240 240">
          {grid.map((d, i) => <polygon key={i} points={d} fill="none" stroke="#e8f0f9" strokeWidth={1} />)}
          {items.map((_, i) => {
            const a = (i / n) * 2 * Math.PI - Math.PI / 2;
            return <line key={i} x1={cx} y1={cy} x2={cx + Math.cos(a) * r} y2={cy + Math.sin(a) * r} stroke="#e8f0f9" strokeWidth={1} />;
          })}
          <polygon points={dataPoints.map(p => p.join(',')).join(' ')} fill="rgba(61,122,181,0.15)" stroke="#3d7ab5" strokeWidth={2} />
          {dataPoints.map((p, i) => <circle key={i} cx={p[0]} cy={p[1]} r={3} fill="#3d7ab5" />)}
          {labelPts.map((p, i) => <text key={i} x={p.x} y={p.y} textAnchor="middle" dominantBaseline="middle" fontSize={10} fill="#64748b" fontFamily="Tajawal">{p.label}</text>)}
        </svg>
      );
    }

    return (
      <div>
        <div style={{ display: 'flex', gap: 8, marginBottom: 16, flexWrap: 'wrap' }}>
          {['steiner', 'downs', 'mcnamara', 'ricketts', 'wits'].map(a => (
            <button key={a} onClick={() => setAnalysis(a)} style={{ padding: '6px 14px', borderRadius: 8, border: `1.5px solid ${analysis === a ? '#3d7ab5' : '#dce8f5'}`, background: analysis === a ? '#3d7ab5' : '#fff', color: analysis === a ? '#fff' : '#64748b', fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600, cursor: 'pointer', textTransform: 'capitalize' }}>
              {a.charAt(0).toUpperCase() + a.slice(1)}
            </button>
          ))}
          <div style={{ marginRight: 'auto' }}>
            <Btn variant="outline" size="sm" icon="ai">تحليل AI</Btn>
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '260px 1fr', gap: 16, marginBottom: 16 }}>
          {/* X-ray placeholder with landmarks */}
          <Card style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
            <div style={{ fontWeight: 700, fontSize: 13, color: '#64748b', marginBottom: 10, alignSelf: 'flex-start' }}>صورة السيفالومتري</div>
            <div style={{ width: 220, height: 260, background: '#0d1520', borderRadius: 10, position: 'relative', overflow: 'hidden', border: '2px solid #1a3a5c' }}>
              {/* Skull silhouette SVG */}
              <svg width="220" height="260" viewBox="0 0 220 260" style={{ position: 'absolute', top: 0, left: 0 }}>
                {/* Cranium outline */}
                <path d="M110 20 C60 20 30 55 28 100 C26 130 32 155 45 170 C50 178 55 185 60 195 C64 202 68 210 68 220 L152 220 C152 210 156 202 160 195 C165 185 170 178 175 170 C188 155 194 130 192 100 C190 55 160 20 110 20Z" fill="none" stroke="rgba(61,122,181,0.35)" strokeWidth="1.5" />
                {/* Maxilla/mandible */}
                <path d="M68 220 C68 220 80 240 110 240 C140 240 152 220 152 220" fill="none" stroke="rgba(61,122,181,0.25)" strokeWidth="1.5" />
                <path d="M60 195 C70 200 90 205 110 205 C130 205 150 200 160 195" fill="none" stroke="rgba(61,122,181,0.2)" strokeWidth="1" />
                {/* Nose profile */}
                <path d="M45 120 C40 115 38 108 42 100 C46 92 55 90 60 95" fill="none" stroke="rgba(61,122,181,0.25)" strokeWidth="1" />
                {/* Reference lines */}
                <line x1="110" y1="30" x2="42" y2="100" stroke="rgba(245,146,46,0.4)" strokeWidth="1" strokeDasharray="4,3" />
                <line x1="110" y1="30" x2="85" y2="165" stroke="rgba(245,146,46,0.4)" strokeWidth="1" strokeDasharray="4,3" />
                {/* Landmark points */}
                {[
                  { x: 110, y: 30, label: 'S', color: '#f5922e' },
                  { x: 87, y: 58, label: 'N', color: '#f5922e' },
                  { x: 75, y: 145, label: 'A', color: '#3d7ab5' },
                  { x: 82, y: 163, label: 'B', color: '#3d7ab5' },
                  { x: 85, y: 175, label: 'Pg', color: '#22c55e' },
                  { x: 78, y: 185, label: 'Me', color: '#22c55e' },
                  { x: 155, y: 168, label: 'Go', color: '#a855f7' },
                  { x: 42, y: 105, label: 'Or', color: '#64748b' },
                ].map(p => (
                  <g key={p.label}>
                    <circle cx={p.x} cy={p.y} r={4} fill={p.color} opacity={0.9} />
                    <text x={p.x + 7} y={p.y + 4} fontSize="9" fill={p.color} fontFamily="monospace">{p.label}</text>
                  </g>
                ))}
                {/* SN plane */}
                <line x1="87" y1="58" x2="110" y2="30" stroke="rgba(245,146,46,0.6)" strokeWidth="1.5" />
              </svg>
              <div style={{ position: 'absolute', bottom: 6, right: 6, fontSize: 9, color: 'rgba(61,122,181,0.6)', fontFamily: 'monospace' }}>Lateral Ceph</div>
            </div>
            <div style={{ marginTop: 12, width: '100%' }}>
              <div style={{ fontSize: 11, color: '#94a3b8', marginBottom: 6 }}>النقاط: 8 / 21 محددة</div>
              <div style={{ height: 4, background: '#f1f5f9', borderRadius: 99 }}>
                <div style={{ width: '38%', height: '100%', background: '#f5922e', borderRadius: 99 }} />
              </div>
            </div>
          </Card>

          {/* Measurements */}
          <Card padding={0}>
            <div style={{ padding: '12px 18px', borderBottom: '1px solid #f1f5f9', fontWeight: 700, fontSize: 14 }}>القياسات — {analysis.charAt(0).toUpperCase() + analysis.slice(1)} Analysis</div>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
              <thead>
                <tr style={{ background: '#f7fafd', borderBottom: '1px solid #e8f0f9' }}>
                  {['القياس', 'القيمة', 'الطبيعي', 'الانحراف', 'التصنيف'].map(h => (
                    <th key={h} style={{ padding: '9px 16px', textAlign: 'right', fontWeight: 700, color: '#64748b', fontSize: 12 }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {measurements.map((m, i) => {
                  const dev = getDeviation(m);
                  const diff = (m.value - m.norm).toFixed(1);
                  return (
                    <tr key={m.name} style={{ borderBottom: '1px solid #f8fafc', background: dev !== 'normal' ? devColor[dev] + '06' : 'transparent' }}>
                      <td style={{ padding: '10px 16px', fontWeight: 700, color: '#0d2137', direction: 'ltr', textAlign: 'right' }}>{m.name}</td>
                      <td style={{ padding: '10px 16px', fontWeight: 800, color: '#0d2137', direction: 'ltr', textAlign: 'right' }}>{m.value}{m.unit}</td>
                      <td style={{ padding: '10px 16px', color: '#94a3b8', direction: 'ltr', textAlign: 'right' }}>{m.norm}±{m.sd}</td>
                      <td style={{ padding: '10px 16px', direction: 'ltr', textAlign: 'right' }}>
                        <span style={{ fontWeight: 700, color: devColor[dev] }}>{diff > 0 ? '+' : ''}{diff}{m.unit}</span>
                      </td>
                      <td style={{ padding: '10px 16px' }}>
                        <Badge color={devColor[dev]}>{devLabel[dev]}</Badge>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </Card>
        </div>

        {/* Diagnosis + Radar */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 260px', gap: 16 }}>
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>التشخيص السيفالومتري</div>
            {[
              ['الفئة الهيكلية', c.cephDiagnosis.skeletalClass],
              ['النمط الرأسي', c.cephDiagnosis.verticalPattern],
              ['ميلان القواطع', c.cephDiagnosis.incisors],
              ['الأنسجة الرخوة', c.cephDiagnosis.softTissue],
            ].map(([k, v]) => (
              <div key={k} style={{ display: 'flex', gap: 10, alignItems: 'flex-start', padding: '8px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                <span style={{ color: '#94a3b8', minWidth: 130 }}>{k}:</span>
                <span style={{ fontWeight: 700, color: '#0d2137' }}>{v}</span>
              </div>
            ))}
            <div style={{ marginTop: 14, padding: 14, background: '#f0f5fb', borderRadius: 10, border: '1px solid #dce8f5' }}>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8 }}>
                <Icon name="ai" size={16} stroke="#3d7ab5" />
                <span style={{ fontWeight: 700, fontSize: 13, color: '#3d7ab5' }}>توصية الذكاء الاصطناعي</span>
                <Badge color="#f5922e">مقترح فقط</Badge>
              </div>
              <p style={{ fontSize: 13, color: '#64748b', lineHeight: 1.7, margin: 0 }}>{c.cephDiagnosis.aiRec}</p>
            </div>
          </Card>
          <Card style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
            <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 10, color: '#64748b', alignSelf: 'flex-start' }}>Polygon Chart</div>
            <RadarChart />
          </Card>
        </div>
      </div>
    );
  }

  // ── Extraction Decision ─────────────────────────────────────────
  function ExtractionTab() {
    const e = c.extractionDecision;
    return (
      <div>
        <Card style={{ marginBottom: 16 }}>
          <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 16 }}>
            <div style={{ fontWeight: 700, fontSize: 16, color: '#0d2137' }}>القرار النهائي:</div>
            <Badge color="#3d7ab5" bg="#e8f0f9">{e.decision}</Badge>
            <div style={{ fontSize: 12, color: '#94a3b8', marginRight: 'auto' }}>اعتمده: د. عقلان الكامل · 18 مارس 2024</div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <div style={{ background: '#f0fdf4', border: '1.5px solid #bbf7d0', borderRadius: 10, padding: 16 }}>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12 }}>
                <div style={{ width: 28, height: 28, borderRadius: 99, background: '#22c55e', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <Icon name="check" size={14} stroke="#fff" strokeWidth={2.5} />
                </div>
                <span style={{ fontWeight: 700, fontSize: 14, color: '#166534' }}>مؤيدات الخلع</span>
              </div>
              {e.proFactors.map((f, i) => (
                <div key={i} style={{ display: 'flex', gap: 8, padding: '6px 0', fontSize: 13, borderBottom: '1px solid #dcfce7', color: '#166534' }}>
                  <span style={{ color: '#22c55e', fontWeight: 700 }}>✓</span>{f}
                </div>
              ))}
            </div>

            <div style={{ background: '#fff7ed', border: '1.5px solid #fed7aa', borderRadius: 10, padding: 16 }}>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12 }}>
                <div style={{ width: 28, height: 28, borderRadius: 99, background: '#f5922e', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <Icon name="x" size={14} stroke="#fff" strokeWidth={2.5} />
                </div>
                <span style={{ fontWeight: 700, fontSize: 14, color: '#9a3412' }}>معارضات الخلع</span>
              </div>
              {e.conFactors.map((f, i) => (
                <div key={i} style={{ display: 'flex', gap: 8, padding: '6px 0', fontSize: 13, borderBottom: '1px solid #fed7aa', color: '#9a3412' }}>
                  <span style={{ color: '#f5922e', fontWeight: 700 }}>—</span>{f}
                </div>
              ))}
            </div>
          </div>
        </Card>

        <Card>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12 }}>
            <Icon name="ai" size={18} stroke="#3d7ab5" />
            <span style={{ fontWeight: 700, fontSize: 14, color: '#3d7ab5' }}>توصية الذكاء الاصطناعي</span>
            <Badge color="#f5922e" bg="#fff7ed">مقترح — القرار للطبيب</Badge>
          </div>
          <div style={{ padding: 16, background: '#f0f5fb', borderRadius: 10, fontSize: 13, color: '#64748b', lineHeight: 1.8 }}>
            {e.aiRecommendation}
          </div>
          <div style={{ display: 'flex', gap: 10, marginTop: 16 }}>
            <Btn variant="primary" size="sm" icon="check">قبول التوصية</Btn>
            <Btn variant="outline" size="sm">تعديل القرار</Btn>
          </div>
        </Card>
      </div>
    );
  }

  // ── Retention ─────────────────────────────────────────
  function RetentionTab() {
    return (
      <div>
        <Card style={{ marginBottom: 16 }}>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', padding: 16, background: '#f0f5fb', borderRadius: 10, marginBottom: 16 }}>
            <Icon name="clock" size={20} stroke="#f5922e" />
            <span style={{ fontSize: 14, fontWeight: 700, color: '#f5922e' }}>العلاج جارٍ — مرحلة Retention لم تبدأ بعد</span>
          </div>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>خطة التثبيت المقترحة</div>
          {[
            ['المثبت العلوي', 'Hawley Retainer — قابل للإزالة'],
            ['المثبت السفلي', 'Bonded Retainer 3-3 — ثابت'],
            ['بروتوكول الارتداء', 'دوام في السنة الأولى ثم ليلاً فقط'],
            ['متابعة Y1', 'كل 3 أشهر'],
            ['متابعة Y2+', 'كل 6 أشهر'],
          ].map(([k, v]) => (
            <div key={k} style={{ display: 'flex', gap: 12, padding: '8px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
              <span style={{ color: '#94a3b8', minWidth: 160 }}>{k}:</span>
              <span style={{ fontWeight: 600, color: '#0d2137' }}>{v}</span>
            </div>
          ))}
        </Card>

        <Card>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>جدول زيارات التثبيت</div>
          {[
            { period: 'الشهر الأول', status: 'pending' },
            { period: 'الشهر الثالث', status: 'pending' },
            { period: 'الشهر السادس', status: 'pending' },
            { period: 'السنة الأولى', status: 'pending' },
            { period: 'السنة الثانية', status: 'pending' },
          ].map((v, i) => (
            <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 14, padding: '10px 0', borderBottom: '1px solid #f8fafc' }}>
              <div style={{ width: 30, height: 30, borderRadius: 99, background: '#f1f5f9', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                <Icon name="calendar" size={14} stroke="#94a3b8" />
              </div>
              <span style={{ flex: 1, fontSize: 13, fontWeight: 600, color: '#0d2137' }}>{v.period}</span>
              <Badge color="#94a3b8">لم يُحدد</Badge>
            </div>
          ))}
        </Card>
      </div>
    );
  }

  const tabContent = {
    overview: <OverviewTab />,
    exam: <ExamTab />,
    problems: <ProblemsTab />,
    plan: <PlanTab />,
    visits: <VisitsTab />,
    stages: <StagesTab />,
    photos: <PhotosTab />,
    ceph: <AdvancedCephPage />,
    extraction: <ExtractionTab />,
    retention: <RetentionTab />,
  };

  return (
    <div>
      {/* Back button */}
      {onBack && (
        <button onClick={onBack} style={{ display: 'flex', alignItems: 'center', gap: 6, background: 'none', border: 'none', cursor: 'pointer', color: '#64748b', fontFamily: 'Tajawal', fontSize: 13, marginBottom: 16, padding: 0, fontWeight: 500 }}>
          ← العودة للمرضى
        </button>
      )}

      {/* Case header */}
      <Card style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', gap: 20, alignItems: 'center' }}>
          <div style={{ width: 56, height: 56, borderRadius: 14, background: '#3d7ab515', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 20, fontWeight: 800, color: '#3d7ab5', flexShrink: 0 }}>
            أ
          </div>
          <div style={{ flex: 1 }}>
            <div style={{ display: 'flex', gap: 10, alignItems: 'center', marginBottom: 6 }}>
              <span style={{ fontSize: 20, fontWeight: 800, color: '#0d2137' }}>{c.patient.name}</span>
              <Badge color="#3d7ab5">تقويم</Badge>
              <Badge color="#22c55e">نشط</Badge>
              <span style={{ fontSize: 12, color: '#94a3b8', marginRight: 4 }}>رقم الحالة: <strong style={{ color: '#3d7ab5' }}>{c.id}</strong></span>
            </div>
            <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap' }}>
              {[
                { k: 'العمر', v: `${c.patient.age} سنة` },
                { k: 'الطبيب', v: c.doctor },
                { k: 'الجهاز', v: c.appliance },
                { k: 'البدء', v: c.startDate },
                { k: 'الزيارة', v: `${c.visits.length}` },
                { k: 'المرحلة', v: `${c.stagePercentage}%` },
              ].map(f => (
                <div key={f.k} style={{ fontSize: 13 }}>
                  <span style={{ color: '#94a3b8' }}>{f.k}: </span>
                  <span style={{ fontWeight: 700, color: '#0d2137' }}>{f.v}</span>
                </div>
              ))}
            </div>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
              <Btn variant="outline" size="sm" icon="report">تقرير PDF</Btn>
              <Btn variant="ghost" size="sm" onClick={() => onPrescription && onPrescription()}>📋 وصفة</Btn>
              <Btn variant="primary" size="sm" icon="plus">زيارة جديدة</Btn>
            </div>
        </div>
      </Card>

      {/* Tabs */}
      <Tabs tabs={TABS} active={tab} onChange={setTab} />

      {/* Content */}
      {tabContent[tab]}
    </div>
  );
}

Object.assign(window, { OrthoPage });
