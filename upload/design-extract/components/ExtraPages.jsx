
// ============================
// Dental Chart + New Patient Form + Prescription
// ============================

// ══════════════════════════════════════
// DENTAL CHART
// ══════════════════════════════════════

function DentalChartPage() {
  const [selectedTooth, setSelectedTooth] = React.useState(null);
  const [toothData, setToothData] = React.useState({});
  const [activeTool, setActiveTool] = React.useState('examine');

  const TOOTH_CONDITIONS = {
    healthy: { label: 'سليم', color: '#22c55e' },
    filling: { label: 'حشوة', color: '#3d7ab5' },
    crown: { label: 'تاج', color: '#f59e0b' },
    extraction: { label: 'خلع مطلوب', color: '#ef4444' },
    extracted: { label: 'مخلوع', color: '#94a3b8' },
    caries: { label: 'نخر', color: '#f5922e' },
    root_canal: { label: 'علاج عصب', color: '#a855f7' },
    implant: { label: 'زرعة', color: '#22c55e' },
    bridge: { label: 'جسر', color: '#0ea5e9' },
    ortho: { label: 'تقويم', color: '#3d7ab5' },
  };

  const TOOLS = [
    { id: 'examine', label: 'فحص', icon: '🔍' },
    { id: 'caries', label: 'نخر', icon: '🔴' },
    { id: 'filling', label: 'حشوة', icon: '🔵' },
    { id: 'crown', label: 'تاج', icon: '🟡' },
    { id: 'extraction', label: 'خلع', icon: '❌' },
    { id: 'root_canal', label: 'عصب', icon: '🟣' },
    { id: 'healthy', label: 'سليم', icon: '🟢' },
    { id: 'erase', label: 'مسح', icon: '⬜' },
  ];

  // Upper teeth: 18-11 (right side) drawn right→center, 21-28 (left side) drawn center→left
  const upperRight = [18, 17, 16, 15, 14, 13, 12, 11]; // patient right, chart right→center
  const upperLeft  = [21, 22, 23, 24, 25, 26, 27, 28]; // patient left, chart center→left
  const lowerLeft  = [31, 32, 33, 34, 35, 36, 37, 38];
  const lowerRight = [48, 47, 46, 45, 44, 43, 42, 41];

  const getToothType = (num) => {
    const n = num % 10;
    if (n === 1 || n === 2) return 'incisor';
    if (n === 3) return 'canine';
    if (n === 4 || n === 5) return 'premolar';
    return 'molar';
  };

  const TOOTH_SHAPES = {
    incisor: { w: 22, h: 28, rx: 4 },
    canine: { w: 20, h: 30, rx: 5 },
    premolar: { w: 22, h: 24, rx: 5 },
    molar: { w: 28, h: 22, rx: 6 },
  };

  const handleToothClick = (num) => {
    if (activeTool === 'examine') {
      setSelectedTooth(num);
    } else if (activeTool === 'erase') {
      setToothData(prev => {
        const copy = { ...prev };
        delete copy[num];
        return copy;
      });
    } else {
      setToothData(prev => ({ ...prev, [num]: activeTool }));
      setSelectedTooth(num);
    }
  };

  function ToothSVG({ num, x, y, upper }) {
    const type = getToothType(num);
    const shape = TOOTH_SHAPES[type];
    const cond = toothData[num];
    const condInfo = cond ? TOOTH_CONDITIONS[cond] : null;
    const isSelected = selectedTooth === num;
    const isExtracted = cond === 'extracted';

    const toothColor = condInfo ? condInfo.color : '#e8f0f9';
    const strokeColor = isSelected ? '#f5922e' : (condInfo ? condInfo.color : '#c8d8ea');

    return (
      <g
        key={num}
        onClick={() => handleToothClick(num)}
        style={{ cursor: 'pointer' }}
        transform={`translate(${x - shape.w / 2}, ${upper ? y - shape.h : y})`}
      >
        {/* Root */}
        {!isExtracted && (
          <path
            d={upper
              ? `M${shape.w * 0.3},0 Q${shape.w * 0.3},-12 ${shape.w * 0.35},-18 M${shape.w * 0.7},0 Q${shape.w * 0.7},-12 ${shape.w * 0.65},-18`
              : `M${shape.w * 0.3},${shape.h} Q${shape.w * 0.3},${shape.h + 12} ${shape.w * 0.35},${shape.h + 18} M${shape.w * 0.7},${shape.h} Q${shape.w * 0.7},${shape.h + 12} ${shape.w * 0.65},${shape.h + 18}`}
            stroke="#e0e8f4" strokeWidth="2.5" fill="none" strokeLinecap="round"
          />
        )}

        {/* Crown */}
        {isExtracted ? (
          <g>
            <line x1={shape.w * 0.2} y1={shape.h * 0.2} x2={shape.w * 0.8} y2={shape.h * 0.8} stroke="#ef4444" strokeWidth="2.5" strokeLinecap="round"/>
            <line x1={shape.w * 0.8} y1={shape.h * 0.2} x2={shape.w * 0.2} y2={shape.h * 0.8} stroke="#ef4444" strokeWidth="2.5" strokeLinecap="round"/>
          </g>
        ) : (
          <rect
            width={shape.w} height={shape.h} rx={shape.rx}
            fill={toothColor} stroke={strokeColor} strokeWidth={isSelected ? 2 : 1.5}
            opacity={isExtracted ? 0.3 : 1}
          />
        )}

        {/* Tooth number */}
        <text
          x={shape.w / 2} y={shape.h / 2 + 4}
          textAnchor="middle" fontSize="8" fontWeight={isSelected ? 700 : 500}
          fill={condInfo && cond !== 'healthy' ? '#fff' : '#64748b'}
          fontFamily="monospace"
        >{num}</text>

        {/* Selected highlight ring */}
        {isSelected && (
          <rect width={shape.w} height={shape.h} rx={shape.rx} fill="none" stroke="#f5922e" strokeWidth="2.5" strokeDasharray="4,2"/>
        )}
      </g>
    );
  }

  // Build tooth positions
  // centerX = 350, each tooth 34px wide
  // upperRight [18..11]: starts at center and goes RIGHT (higher numbers further right)
  // upperLeft  [21..28]: starts at center and goes LEFT
  // lowerLeft  [31..38]: starts at center and goes LEFT
  // lowerRight [48..41]: starts at center and goes RIGHT
  function buildRow(teeth, y, upper, fromCenter, direction) {
    // direction: 1 = rightward from center, -1 = leftward from center
    const CX = 350;
    const STEP = 34;
    return teeth.map((num, i) => {
      const x = direction === 1
        ? CX + (i + 0.5) * STEP
        : CX - (i + 0.5) * STEP;
      return <ToothSVG key={num} num={num} x={x} y={y} upper={upper} />;
    });
  }

  // Condition counts
  const counts = {};
  Object.values(toothData).forEach(c => { counts[c] = (counts[c] || 0) + 1; });

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 280px', gap: 16 }}>
        {/* Chart */}
        <Card padding={0}>
          <div style={{ padding: '14px 20px', borderBottom: '1px solid #f1f5f9', display: 'flex', gap: 10, alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ fontWeight: 800, fontSize: 15, color: '#0d2137' }}>مخطط الأسنان التفاعلي</div>
            <div style={{ display: 'flex', gap: 8 }}>
              <Btn variant="outline" size="sm" icon="report">طباعة</Btn>
              <Btn variant="primary" size="sm" icon="plus">إضافة ملاحظة</Btn>
            </div>
          </div>

          {/* Tools */}
          <div style={{ padding: '12px 20px', borderBottom: '1px solid #f1f5f9', display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {TOOLS.map(t => (
              <button key={t.id} onClick={() => setActiveTool(t.id)} style={{
                padding: '5px 12px', borderRadius: 8,
                border: `1.5px solid ${activeTool === t.id ? '#3d7ab5' : '#e8f0f9'}`,
                background: activeTool === t.id ? '#3d7ab5' : '#fff',
                color: activeTool === t.id ? '#fff' : '#64748b',
                fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600, cursor: 'pointer',
                display: 'flex', gap: 4, alignItems: 'center',
              }}>
                <span>{t.icon}</span>{t.label}
              </button>
            ))}
          </div>

          {/* SVG Chart */}
          <div style={{ padding: '20px', overflowX: 'auto' }}>
            <svg width="700" height="230" viewBox="0 0 700 230" style={{ display: 'block', margin: '0 auto', maxWidth: '100%' }}>
              {/* Labels */}
              <text x="350" y="14" textAnchor="middle" fontSize="11" fill="#94a3b8" fontFamily="Tajawal">— فك علوي —</text>
              <text x="350" y="222" textAnchor="middle" fontSize="11" fill="#94a3b8" fontFamily="Tajawal">— فك سفلي —</text>
              {/* Quadrant labels */}
              <text x="180" y="30" textAnchor="middle" fontSize="9" fill="#cbd5e1" fontFamily="Tajawal">Q1 — يمين علوي</text>
              <text x="520" y="30" textAnchor="middle" fontSize="9" fill="#cbd5e1" fontFamily="Tajawal">Q2 — يسار علوي</text>
              <text x="180" y="218" textAnchor="middle" fontSize="9" fill="#cbd5e1" fontFamily="Tajawal">Q4 — يمين سفلي</text>
              <text x="520" y="218" textAnchor="middle" fontSize="9" fill="#cbd5e1" fontFamily="Tajawal">Q3 — يسار سفلي</text>
              {/* Midline */}
              <line x1="350" y1="18" x2="350" y2="210" stroke="#f1f5f9" strokeWidth="1.5" strokeDasharray="4,3"/>
              {/* Gum band */}
              <rect x="16" y="100" width="668" height="30" rx="15" fill="#ffe4e6" opacity="0.35"/>
              <text x="8" y="119" fontSize="9" fill="#fca5a5" fontFamily="Tajawal">لثة</text>

              {/* Upper right [18..11]: center rightward */}
              {buildRow(upperRight, 100, true, true, 1)}
              {/* Upper left [21..28]: center leftward */}
              {buildRow(upperLeft, 100, true, true, -1)}
              {/* Lower left [31..38]: center leftward */}
              {buildRow(lowerLeft, 130, false, true, -1)}
              {/* Lower right [48..41]: center rightward */}
              {buildRow(lowerRight, 130, false, true, 1)}
            </svg>
          </div>

          {/* Legend */}
          <div style={{ padding: '0 20px 16px', display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {Object.entries(TOOTH_CONDITIONS).map(([key, val]) => (
              <div key={key} style={{ display: 'flex', gap: 4, alignItems: 'center', fontSize: 11, color: '#64748b' }}>
                <div style={{ width: 10, height: 10, borderRadius: 3, background: val.color }} />
                {val.label}
              </div>
            ))}
          </div>
        </Card>

        {/* Details panel */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          {/* Selected tooth */}
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 12 }}>
              {selectedTooth ? `سن رقم ${selectedTooth}` : 'اختر سناً للفحص'}
            </div>
            {selectedTooth ? (
              <div>
                <div style={{ marginBottom: 12 }}>
                  {['molar','molar','molar','premolar','premolar','canine','incisor','incisor'].includes(getToothType(selectedTooth))
                    && <div style={{ fontSize: 12, color: '#64748b', marginBottom: 8 }}>
                      النوع: {getToothType(selectedTooth) === 'molar' ? 'ضرس' : getToothType(selectedTooth) === 'premolar' ? 'ضاحك' : getToothType(selectedTooth) === 'canine' ? 'ناب' : 'قاطعة'}
                    </div>
                  }
                  <div style={{ fontSize: 12, color: '#64748b', marginBottom: 8 }}>
                    الحالة: {toothData[selectedTooth] ? TOOTH_CONDITIONS[toothData[selectedTooth]]?.label : 'لم يُفحص'}
                  </div>
                </div>
                <div style={{ fontSize: 12, fontWeight: 600, color: '#64748b', marginBottom: 8 }}>تغيير الحالة:</div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  {Object.entries(TOOTH_CONDITIONS).map(([key, val]) => (
                    <button key={key} onClick={() => { setToothData(prev => ({...prev, [selectedTooth]: key})); }} style={{
                      display: 'flex', gap: 8, alignItems: 'center', padding: '6px 10px', borderRadius: 8,
                      border: `1.5px solid ${toothData[selectedTooth] === key ? val.color : '#e8f0f9'}`,
                      background: toothData[selectedTooth] === key ? val.color + '18' : '#fff',
                      cursor: 'pointer', fontFamily: 'Tajawal', fontSize: 12, fontWeight: 600, color: val.color,
                    }}>
                      <div style={{ width: 10, height: 10, borderRadius: 3, background: val.color, flexShrink: 0 }} />
                      {val.label}
                    </button>
                  ))}
                </div>
                <div style={{ marginTop: 12 }}>
                  <textarea placeholder="ملاحظة على هذا السن..." style={{
                    width: '100%', padding: '8px 10px', borderRadius: 8,
                    border: '1.5px solid #e8f0f9', fontFamily: 'Tajawal', fontSize: 12,
                    resize: 'none', outline: 'none', color: '#0d2137',
                  }} rows={3} />
                </div>
              </div>
            ) : (
              <div style={{ fontSize: 13, color: '#94a3b8', textAlign: 'center', padding: '20px 0' }}>
                انقر على أي سن في المخطط لرؤية تفاصيله وتعديل حالته
              </div>
            )}
          </Card>

          {/* Summary */}
          <Card>
            <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 12 }}>ملخص الحالة السنية</div>
            {Object.keys(counts).length === 0 ? (
              <div style={{ fontSize: 13, color: '#94a3b8' }}>لم تُسجَّل أي حالات بعد</div>
            ) : Object.entries(counts).map(([key, count]) => (
              <div key={key} style={{ display: 'flex', justifyContent: 'space-between', padding: '5px 0', borderBottom: '1px solid #f8fafc', fontSize: 13 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <div style={{ width: 10, height: 10, borderRadius: 3, background: TOOTH_CONDITIONS[key]?.color }} />
                  <span style={{ color: '#0d2137' }}>{TOOTH_CONDITIONS[key]?.label}</span>
                </div>
                <span style={{ fontWeight: 700, color: TOOTH_CONDITIONS[key]?.color }}>{count} سن</span>
              </div>
            ))}
            <div style={{ marginTop: 12 }}>
              <Btn variant="primary" size="sm" icon="report" style={{ width: '100%', justifyContent: 'center' }}>حفظ المخطط</Btn>
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
}

// ══════════════════════════════════════
// NEW PATIENT FORM MODAL
// ══════════════════════════════════════

function NewPatientModal({ onClose, onSave }) {
  const [step, setStep] = React.useState(1);
  const [form, setForm] = React.useState({
    name: '', dob: '', gender: '', phone: '', phone2: '', email: '',
    address: '', occupation: '', referral: '',
    doctor: '', specialty: '', chief_complaint: '',
    medical_history: '', allergies: '', medications: '',
    breathingIssues: false, grinding: false, thumbSucking: false,
  });

  const set = (k, v) => setForm(prev => ({...prev, [k]: v}));

  const Field = ({ label, id, type = 'text', options, placeholder, half }) => (
    <div style={{ flex: half ? '0 0 calc(50% - 6px)' : '1 1 100%', marginBottom: 14 }}>
      <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#64748b', marginBottom: 5 }}>{label}</label>
      {options ? (
        <select value={form[id]} onChange={e => set(id, e.target.value)} style={{
          width: '100%', padding: '9px 12px', borderRadius: 8,
          border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13,
          outline: 'none', background: '#fff', color: '#0d2137',
        }}>
          <option value="">اختر...</option>
          {options.map(o => <option key={o} value={o}>{o}</option>)}
        </select>
      ) : (
        <input
          type={type} value={form[id]} onChange={e => set(id, e.target.value)}
          placeholder={placeholder}
          style={{
            width: '100%', padding: '9px 12px', borderRadius: 8,
            border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13,
            outline: 'none', color: '#0d2137',
          }}
        />
      )}
    </div>
  );

  const Toggle = ({ label, id }) => (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 0', borderBottom: '1px solid #f8fafc' }}>
      <span style={{ fontSize: 13, color: '#0d2137' }}>{label}</span>
      <button onClick={() => set(id, !form[id])} style={{
        width: 44, height: 24, borderRadius: 99, border: 'none', cursor: 'pointer',
        background: form[id] ? '#3d7ab5' : '#e2e8f0', transition: 'background 0.2s',
        position: 'relative',
      }}>
        <div style={{
          position: 'absolute', top: 3, left: form[id] ? 22 : 3,
          width: 18, height: 18, borderRadius: 99, background: '#fff',
          boxShadow: '0 1px 3px rgba(0,0,0,0.2)', transition: 'left 0.2s',
        }} />
      </button>
    </div>
  );

  const STEPS = ['البيانات الشخصية', 'التخصص والطبيب', 'التاريخ الطبي'];

  return (
    <div style={{
      position: 'fixed', inset: 0, background: 'rgba(13,33,55,0.6)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      zIndex: 1000, backdropFilter: 'blur(4px)',
    }}>
      <div style={{
        width: 640, background: '#fff', borderRadius: 20,
        boxShadow: '0 20px 60px rgba(13,33,55,0.2)',
        maxHeight: '90vh', display: 'flex', flexDirection: 'column',
        animation: 'fadeIn 0.25s ease',
      }}>
        {/* Header */}
        <div style={{ padding: '20px 24px', borderBottom: '1px solid #f1f5f9', display: 'flex', alignItems: 'center', gap: 14 }}>
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 18, fontWeight: 800, color: '#0d2137' }}>تسجيل مريض جديد</div>
            <div style={{ fontSize: 12, color: '#94a3b8', marginTop: 2 }}>الخطوة {step} من {STEPS.length}: {STEPS[step - 1]}</div>
          </div>
          {/* Step indicators */}
          <div style={{ display: 'flex', gap: 6 }}>
            {STEPS.map((s, i) => (
              <div key={i} style={{
                width: i < step - 1 ? 28 : 8, height: 8, borderRadius: 99,
                background: i < step ? '#3d7ab5' : '#e8f0f9',
                transition: 'all 0.3s',
              }} />
            ))}
          </div>
          <button onClick={onClose} style={{ background: '#f1f5f9', border: 'none', borderRadius: 8, width: 32, height: 32, cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Icon name="x" size={16} stroke="#64748b" />
          </button>
        </div>

        {/* Form content */}
        <div style={{ flex: 1, overflowY: 'auto', padding: 24 }}>
          {step === 1 && (
            <div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12 }}>
                <div style={{ flex: '1 1 100%' }}><Field label="الاسم الرباعي *" id="name" placeholder="أحمد محمد علي الشيباني" /></div>
                <div style={{ flex: '0 0 calc(50% - 6px)' }}><Field label="تاريخ الميلاد" id="dob" type="date" /></div>
                <div style={{ flex: '0 0 calc(50% - 6px)' }}><Field label="الجنس" id="gender" options={['ذكر', 'أنثى']} /></div>
                <div style={{ flex: '0 0 calc(50% - 6px)' }}><Field label="رقم الهاتف *" id="phone" placeholder="770-XXXXXX" type="tel" /></div>
                <div style={{ flex: '0 0 calc(50% - 6px)' }}><Field label="هاتف بديل" id="phone2" placeholder="711-XXXXXX" type="tel" /></div>
                <div style={{ flex: '1 1 100%' }}><Field label="البريد الإلكتروني" id="email" type="email" placeholder="example@email.com" /></div>
                <div style={{ flex: '1 1 100%' }}><Field label="العنوان" id="address" placeholder="المحافظة — الحي — الشارع" /></div>
                <div style={{ flex: '0 0 calc(50% - 6px)' }}><Field label="المهنة" id="occupation" placeholder="مثال: طالب، موظف..." /></div>
                <div style={{ flex: '0 0 calc(50% - 6px)' }}><Field label="مصدر الإحالة" id="referral" options={['توصية شخصية', 'وسائل التواصل', 'الإنترنت', 'لافتة إعلانية', 'إحالة طبية', 'أخرى']} /></div>
              </div>
            </div>
          )}

          {step === 2 && (
            <div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12 }}>
                <div style={{ flex: '0 0 calc(50% - 6px)' }}><Field label="التخصص *" id="specialty" options={['تقويم أسنان', 'طب أسنان عام', 'جراحة وجه وفكين', 'لثة وعظم', 'أطفال']} /></div>
                <div style={{ flex: '0 0 calc(50% - 6px)' }}><Field label="الطبيب المعالج" id="doctor" options={['د. عقلان الكامل', 'د. عائشة غازي', 'د. إيمان الكامل', 'د. هشام القدسي', 'د. خلدون البريهي']} /></div>
                <div style={{ flex: '1 1 100%' }}>
                  <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#64748b', marginBottom: 5 }}>الشكوى الرئيسية *</label>
                  <textarea value={form.chief_complaint} onChange={e => set('chief_complaint', e.target.value)}
                    placeholder="اكتب الشكوى الرئيسية للمريض..."
                    style={{ width: '100%', padding: '9px 12px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none', resize: 'none', color: '#0d2137' }}
                    rows={3}
                  />
                </div>
              </div>

              {/* Login credentials generation */}
              <div style={{ background: '#f0f5fb', borderRadius: 12, padding: 16, marginTop: 8 }}>
                <div style={{ fontWeight: 700, fontSize: 13, color: '#3d7ab5', marginBottom: 10 }}>🔑 بيانات دخول بوابة المريض</div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                  <div>
                    <div style={{ fontSize: 11, color: '#64748b', marginBottom: 4 }}>اسم المستخدم (تلقائي)</div>
                    <div style={{ padding: '8px 12px', background: '#fff', borderRadius: 8, border: '1px solid #dce8f5', fontSize: 12, fontFamily: 'monospace', color: '#0d2137' }}>
                      {form.name ? form.name.split(' ')[0].toLowerCase() + '.' + (form.phone.replace('-', '').slice(-4) || '0000') : 'اسم.XXXX'}
                    </div>
                  </div>
                  <div>
                    <div style={{ fontSize: 11, color: '#64748b', marginBottom: 4 }}>كلمة المرور المؤقتة</div>
                    <div style={{ padding: '8px 12px', background: '#fff', borderRadius: 8, border: '1px solid #dce8f5', fontSize: 12, fontFamily: 'monospace', color: '#3d7ab5', fontWeight: 700 }}>
                      {form.phone ? form.phone.slice(-4) : 'XXXX'}
                    </div>
                  </div>
                </div>
                <div style={{ fontSize: 11, color: '#94a3b8', marginTop: 8 }}>سيتم إرسال بيانات الدخول للمريض عبر SMS عند التسجيل</div>
              </div>
            </div>
          )}

          {step === 3 && (
            <div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12 }}>
                <div style={{ flex: '1 1 100%' }}>
                  <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#64748b', marginBottom: 5 }}>الأمراض المزمنة والتاريخ الطبي</label>
                  <textarea value={form.medical_history} onChange={e => set('medical_history', e.target.value)}
                    placeholder="مرض السكري، ضغط الدم، أمراض القلب، الاضطرابات النزفية..."
                    style={{ width: '100%', padding: '9px 12px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none', resize: 'none', color: '#0d2137' }}
                    rows={2}
                  />
                </div>
                <div style={{ flex: '0 0 calc(50% - 6px)' }}><Field label="الحساسية الدوائية" id="allergies" placeholder="مثال: البنسلين، السلفا..." /></div>
                <div style={{ flex: '0 0 calc(50% - 6px)' }}><Field label="الأدوية الحالية" id="medications" placeholder="اكتب أسماء الأدوية..." /></div>
              </div>
              <div style={{ marginTop: 8 }}>
                <div style={{ fontSize: 13, fontWeight: 700, color: '#0d2137', marginBottom: 12 }}>العادات الفموية</div>
                <Toggle label="التنفس الفموي" id="breathingIssues" />
                <Toggle label="الطحن الليلي (Bruxism)" id="grinding" />
                <Toggle label="مص الإبهام أو الأقلام" id="thumbSucking" />
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div style={{ padding: '16px 24px', borderTop: '1px solid #f1f5f9', display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
          {step > 1 && <Btn variant="outline" onClick={() => setStep(s => s - 1)}>السابق</Btn>}
          <Btn variant="ghost" onClick={onClose}>إلغاء</Btn>
          {step < STEPS.length ? (
            <Btn variant="primary" icon="arrow_right" onClick={() => setStep(s => s + 1)}>التالي</Btn>
          ) : (
            <Btn variant="primary" icon="check" onClick={() => { onSave && onSave(form); onClose(); }}>
              حفظ وتسجيل المريض
            </Btn>
          )}
        </div>
      </div>
    </div>
  );
}

// ══════════════════════════════════════
// PRESCRIPTION
// ══════════════════════════════════════

function PrescriptionModal({ patient, onClose }) {
  const [meds, setMeds] = React.useState([
    { name: 'Amoxicillin 500mg', dose: 'كبسولة', freq: '3×يومياً', duration: '5 أيام', notes: '' },
  ]);
  const [diagnosis, setDiagnosis] = React.useState('');
  const [notes, setNotes] = React.useState('');

  const addMed = () => setMeds(prev => [...prev, { name: '', dose: '', freq: '', duration: '', notes: '' }]);
  const removeMed = (i) => setMeds(prev => prev.filter((_, idx) => idx !== i));
  const updateMed = (i, k, v) => setMeds(prev => prev.map((m, idx) => idx === i ? {...m, [k]: v} : m));

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(13,33,55,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
      <div style={{ width: 660, background: '#fff', borderRadius: 20, boxShadow: '0 20px 60px rgba(13,33,55,0.2)', maxHeight: '90vh', display: 'flex', flexDirection: 'column' }}>
        {/* Header */}
        <div style={{ padding: '20px 24px', borderBottom: '1px solid #f1f5f9', display: 'flex', gap: 14, alignItems: 'center' }}>
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 18, fontWeight: 800, color: '#0d2137' }}>📋 وصفة طبية إلكترونية</div>
            {patient && <div style={{ fontSize: 13, color: '#64748b', marginTop: 2 }}>المريض: {patient.name} · {patient.number}</div>}
          </div>
          <button onClick={onClose} style={{ background: '#f1f5f9', border: 'none', borderRadius: 8, width: 32, height: 32, cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Icon name="x" size={16} stroke="#64748b" />
          </button>
        </div>

        <div style={{ flex: 1, overflowY: 'auto', padding: 24 }}>
          {/* Clinic header */}
          <div style={{ background: '#0d2137', borderRadius: 12, padding: 16, marginBottom: 20, display: 'flex', gap: 14, alignItems: 'center' }}>
            <img src="uploads/logo_upload-1777339394562.png" style={{ width: 44, height: 44, objectFit: 'contain', background: '#fff', borderRadius: 8, padding: 2 }} alt="Aqlan" />
            <div>
              <div style={{ color: '#fff', fontWeight: 800, fontSize: 14 }}>مركز د. عقلان الكامل لطب وتقويم الأسنان</div>
              <div style={{ color: 'rgba(255,255,255,0.5)', fontSize: 12 }}>تعز · 04-253028 · 770-245745</div>
            </div>
            <div style={{ marginRight: 'auto', textAlign: 'left' }}>
              <div style={{ color: 'rgba(255,255,255,0.5)', fontSize: 11 }}>التاريخ</div>
              <div style={{ color: '#fff', fontSize: 13, fontWeight: 700 }}>28 أكتوبر 2024</div>
            </div>
          </div>

          {/* Diagnosis */}
          <div style={{ marginBottom: 16 }}>
            <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#64748b', marginBottom: 5 }}>التشخيص</label>
            <input value={diagnosis} onChange={e => setDiagnosis(e.target.value)}
              placeholder="مثال: التهاب حول العلاج التقويمي..."
              style={{ width: '100%', padding: '9px 12px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none' }}
            />
          </div>

          {/* Medications */}
          <div style={{ fontWeight: 700, fontSize: 13, color: '#0d2137', marginBottom: 12 }}>الأدوية</div>
          {meds.map((med, i) => (
            <div key={i} style={{ background: '#f7fafd', borderRadius: 12, padding: 14, marginBottom: 10, border: '1.5px solid #e8f0f9' }}>
              <div style={{ display: 'flex', gap: 8, marginBottom: 10, alignItems: 'center' }}>
                <div style={{ width: 26, height: 26, borderRadius: 99, background: '#3d7ab5', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700, color: '#fff', flexShrink: 0 }}>{i + 1}</div>
                <input value={med.name} onChange={e => updateMed(i, 'name', e.target.value)}
                  placeholder="اسم الدواء والتركيز..."
                  style={{ flex: 1, padding: '7px 10px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none', background: '#fff', fontWeight: 600 }}
                />
                <button onClick={() => removeMed(i)} style={{ background: '#fef2f2', border: 'none', borderRadius: 8, width: 30, height: 30, cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <Icon name="trash" size={13} stroke="#ef4444" />
                </button>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8 }}>
                {[['dose','الجرعة','مثال: قرص واحد'],['freq','التكرار','مثال: 3× يومياً'],['duration','المدة','مثال: 5 أيام']].map(([k, lbl, ph]) => (
                  <div key={k}>
                    <div style={{ fontSize: 10, color: '#94a3b8', marginBottom: 3 }}>{lbl}</div>
                    <input value={med[k]} onChange={e => updateMed(i, k, e.target.value)} placeholder={ph}
                      style={{ width: '100%', padding: '6px 8px', borderRadius: 6, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 12, outline: 'none', background: '#fff' }}
                    />
                  </div>
                ))}
              </div>
            </div>
          ))}
          <button onClick={addMed} style={{ display: 'flex', gap: 6, alignItems: 'center', padding: '8px 14px', borderRadius: 8, border: '1.5px dashed #3d7ab5', background: '#f0f5fb', cursor: 'pointer', fontFamily: 'Tajawal', fontSize: 13, fontWeight: 600, color: '#3d7ab5', marginBottom: 16 }}>
            <Icon name="plus" size={15} stroke="#3d7ab5" /> إضافة دواء
          </button>

          {/* Notes */}
          <div>
            <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#64748b', marginBottom: 5 }}>تعليمات وملاحظات</label>
            <textarea value={notes} onChange={e => setNotes(e.target.value)}
              placeholder="تعليمات خاصة للمريض..."
              style={{ width: '100%', padding: '9px 12px', borderRadius: 8, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none', resize: 'none' }}
              rows={2}
            />
          </div>
        </div>

        {/* Footer */}
        <div style={{ padding: '16px 24px', borderTop: '1px solid #f1f5f9', display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
          <Btn variant="ghost" onClick={onClose}>إلغاء</Btn>
          <Btn variant="outline" icon="report">طباعة PDF</Btn>
          <Btn variant="primary" icon="check" onClick={onClose}>حفظ الوصفة</Btn>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { DentalChartPage, NewPatientModal, PrescriptionModal });
