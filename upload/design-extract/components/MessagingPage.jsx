
// ============================
// Messaging Page — Aqlan Dental Pro
// ============================

function MessagingPage() {
  const [activeConv, setActiveConv] = React.useState(null);
  const [tab, setTab] = React.useState('all'); // all | doctors | patients
  const [messages, setMessages] = React.useState({});
  const [input, setInput] = React.useState('');
  const bottomRef = React.useRef(null);

  // Conversations data
  const CONVERSATIONS = [
    {
      id: 'patient_P001', type: 'patient',
      name: 'أحمد محمد الشيباني', sub: 'OR-2024-108 · تقويم',
      initials: 'أ', color: '#3d7ab5',
      lastMsg: 'شكراً جزيلاً! هل يمكن تأجيل الموعد؟', lastTime: '10:15 ص',
      unread: 2, online: true,
    },
    {
      id: 'patient_P004', type: 'patient',
      name: 'نورة عبدالله القحطاني', sub: 'OR-2024-115 · تقويم',
      initials: 'ن', color: '#a855f7',
      lastMsg: 'كيف أتعامل مع الـ Elastics؟', lastTime: 'أمس',
      unread: 1, online: false,
    },
    {
      id: 'patient_P006', type: 'patient',
      name: 'ريم طارق المحاوي', sub: 'OR-2024-119 · تقويم',
      initials: 'ر', color: '#f5922e',
      lastMsg: 'تم استلام الوصفة شكراً', lastTime: 'أمس',
      unread: 0, online: true,
    },
    {
      id: 'doctor_2', type: 'doctor',
      name: 'د. عائشة غازي', sub: 'طب أسنان عام',
      initials: 'عغ', color: '#f5922e',
      lastMsg: 'حسناً سأراجع ملف المريض وأرد عليك', lastTime: '09:00 ص',
      unread: 0, online: true,
    },
    {
      id: 'doctor_3', type: 'doctor',
      name: 'د. إيمان الكامل', sub: 'طب أسنان عام',
      initials: 'إك', color: '#22c55e',
      lastMsg: 'صباح النور، المريض يحتاج إحالة لك', lastTime: 'أمس',
      unread: 1, online: false,
    },
    {
      id: 'group_team', type: 'group',
      name: 'الفريق الطبي — كل الأطباء', sub: '5 أعضاء',
      initials: '👥', color: '#0d2137',
      lastMsg: 'تذكير: اجتماع الفريق الأسبوعي غداً 2م', lastTime: 'أمس',
      unread: 3, online: false,
    },
  ];

  const INIT_MESSAGES = {
    patient_P001: [
      { id: 1, from: 'clinic', sender: 'ريم (الاستقبال)', text: 'أهلاً أحمد! مرحباً بك في نظام الرسائل. يمكنك التواصل معنا في أي وقت خلال أوقات العمل.', time: '09:00 ص', date: '28 أكتوبر 2024' },
      { id: 2, from: 'patient', text: 'شكراً جزيلاً! عندي استفسار — هل يمكن تأجيل موعد 3 ديسمبر لأسبوع؟', time: '10:15 ص', date: '28 أكتوبر 2024' },
      { id: 3, from: 'clinic', sender: 'د. عقلان الكامل', text: 'أحمد، يمكن تأجيل الموعد لـ 10 ديسمبر في نفس التوقيت. هل يناسبك؟', time: '11:00 ص', date: '28 أكتوبر 2024' },
      { id: 4, from: 'patient', text: 'نعم ممتاز يناسبني! شكراً دكتور.', time: '11:30 ص', date: '28 أكتوبر 2024' },
    ],
    patient_P004: [
      { id: 1, from: 'patient', text: 'السلام عليكم دكتور، كيف أتعامل مع الـ Elastics؟ ارتديتها وأحس بألم.', time: '03:00 م', date: '27 أكتوبر 2024' },
      { id: 2, from: 'clinic', sender: 'د. عقلان الكامل', text: 'وعليكم السلام نورة! الألم طبيعي في البداية. ارتديها 22 ساعة يومياً وغيّريها عند الانقطاع. بعد أسبوع ستعتادين عليها.', time: '05:00 م', date: '27 أكتوبر 2024' },
    ],
    doctor_2: [
      { id: 1, from: 'me', sender: 'د. عقلان الكامل', text: 'مرحباً عائشة، المريض فاطمة المقطري تحتاج تنسيق بين علاج العصب وعلاج التقويم. هل يمكنك مراجعة ملفها؟', time: '08:30 ص', date: '28 أكتوبر 2024' },
      { id: 2, from: 'other', sender: 'د. عائشة غازي', text: 'حسناً سأراجع ملف المريض وأرد عليك خلال اليوم.', time: '09:00 ص', date: '28 أكتوبر 2024' },
    ],
    group_team: [
      { id: 1, from: 'other', sender: 'ريم (الاستقبال)', text: 'تذكير للفريق: اجتماع الأسبوعي غداً الساعة 2م في قاعة الاجتماعات.', time: '10:00 ص', date: '27 أكتوبر 2024' },
      { id: 2, from: 'other', sender: 'د. عائشة غازي', text: '✅ حاضر', time: '10:15 ص', date: '27 أكتوبر 2024' },
      { id: 3, from: 'other', sender: 'د. إيمان الكامل', text: '✅ حاضر', time: '10:20 ص', date: '27 أكتوبر 2024' },
    ],
  };

  const [convMessages, setConvMessages] = React.useState(INIT_MESSAGES);

  React.useEffect(() => {
    if (bottomRef.current) {
      bottomRef.current.scrollIntoView ? null : null;
      const el = bottomRef.current.parentElement;
      if (el) el.scrollTop = el.scrollHeight;
    }
  }, [activeConv, convMessages]);

  const sendMsg = () => {
    if (!input.trim() || !activeConv) return;
    const newMsg = {
      id: Date.now(), from: 'me',
      sender: 'د. عقلان الكامل',
      text: input.trim(),
      time: new Date().toLocaleTimeString('ar', { hour: '2-digit', minute: '2-digit' }),
      date: 'الآن',
    };
    setConvMessages(prev => ({
      ...prev,
      [activeConv]: [...(prev[activeConv] || []), newMsg],
    }));
    setInput('');
  };

  const filtered = CONVERSATIONS.filter(c => {
    if (tab === 'doctors') return c.type === 'doctor' || c.type === 'group';
    if (tab === 'patients') return c.type === 'patient';
    return true;
  });

  const conv = CONVERSATIONS.find(c => c.id === activeConv);
  const msgs = convMessages[activeConv] || [];

  // ── Conversation List ──
  function ConvList() {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        {/* Search */}
        <div style={{ padding: '0 0 16px' }}>
          <div style={{ position: 'relative' }}>
            <input placeholder="بحث في الرسائل..." style={{ width: '100%', padding: '9px 36px 9px 12px', borderRadius: 10, border: '1.5px solid #dce8f5', fontFamily: 'Tajawal', fontSize: 13, outline: 'none', background: '#f7fafd', direction: 'rtl' }} />
            <span style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: '#94a3b8' }}>
              <Icon name="search" size={15} />
            </span>
          </div>
        </div>

        {/* Tabs */}
        <div style={{ display: 'flex', gap: 6, marginBottom: 14 }}>
          {[['all', 'الكل'], ['patients', 'المرضى'], ['doctors', 'الأطباء']].map(([val, lbl]) => (
            <button key={val} onClick={() => setTab(val)} style={{
              padding: '6px 14px', borderRadius: 8, border: 'none',
              background: tab === val ? '#3d7ab5' : '#f0f5fb',
              color: tab === val ? '#fff' : '#64748b',
              fontFamily: 'Tajawal', fontWeight: 600, fontSize: 12, cursor: 'pointer',
            }}>{lbl}</button>
          ))}
          <div style={{ marginRight: 'auto' }}>
            <Btn variant="primary" size="sm" icon="plus">رسالة جديدة</Btn>
          </div>
        </div>

        {/* List */}
        <div style={{ flex: 1, overflowY: 'auto' }}>
          {filtered.map(c => (
            <div key={c.id}
              onClick={() => setActiveConv(c.id)}
              style={{
                display: 'flex', gap: 12, padding: '12px 14px', borderRadius: 12,
                background: activeConv === c.id ? '#3d7ab515' : 'transparent',
                cursor: 'pointer', border: activeConv === c.id ? '1.5px solid #3d7ab530' : '1.5px solid transparent',
                marginBottom: 4, transition: 'all 0.15s',
              }}
              onMouseEnter={e => { if (activeConv !== c.id) e.currentTarget.style.background = '#f7fafd'; }}
              onMouseLeave={e => { if (activeConv !== c.id) e.currentTarget.style.background = 'transparent'; }}
            >
              <div style={{ position: 'relative', flexShrink: 0 }}>
                <div style={{ width: 44, height: 44, borderRadius: 99, background: c.color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: c.type === 'group' ? 20 : 14, fontWeight: 700, color: '#fff' }}>
                  {c.type === 'group' ? c.initials : c.initials}
                </div>
                {c.online && <div style={{ position: 'absolute', bottom: 1, right: 1, width: 10, height: 10, borderRadius: 99, background: '#22c55e', border: '2px solid #fff' }} />}
              </div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 3 }}>
                  <span style={{ fontWeight: 700, fontSize: 14, color: '#0d2137' }}>{c.name}</span>
                  <span style={{ fontSize: 11, color: '#94a3b8', flexShrink: 0 }}>{c.lastTime}</span>
                </div>
                <div style={{ fontSize: 12, color: '#94a3b8', marginBottom: 3, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{c.lastMsg}</div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontSize: 11, color: '#cbd5e1' }}>{c.sub}</span>
                  {c.unread > 0 && <span style={{ background: '#3d7ab5', color: '#fff', borderRadius: 99, padding: '1px 7px', fontSize: 11, fontWeight: 700 }}>{c.unread}</span>}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    );
  }

  // ── Chat Window ──
  function ChatWindow() {
    if (!conv) return (
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 14, color: '#94a3b8' }}>
        <div style={{ fontSize: 48 }}>💬</div>
        <div style={{ fontWeight: 700, fontSize: 16 }}>اختر محادثة</div>
        <div style={{ fontSize: 13, textAlign: 'center', maxWidth: 260, lineHeight: 1.7 }}>اختر محادثة من القائمة على اليسار أو ابدأ محادثة جديدة</div>
      </div>
    );

    return (
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        {/* Chat header */}
        <div style={{ padding: '14px 20px', borderBottom: '1px solid #e8f0f9', display: 'flex', gap: 12, alignItems: 'center', background: '#fff', flexShrink: 0 }}>
          <button onClick={() => setActiveConv(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#64748b', padding: 4 }}>
            <Icon name="chevronLeft" size={18} />
          </button>
          <div style={{ width: 38, height: 38, borderRadius: 99, background: conv.color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 13, fontWeight: 700, color: '#fff', flexShrink: 0, position: 'relative' }}>
            {conv.initials}
            {conv.online && <div style={{ position: 'absolute', bottom: 0, right: 0, width: 10, height: 10, borderRadius: 99, background: '#22c55e', border: '2px solid #fff' }} />}
          </div>
          <div style={{ flex: 1 }}>
            <div style={{ fontWeight: 700, fontSize: 14, color: '#0d2137' }}>{conv.name}</div>
            <div style={{ fontSize: 11, color: conv.online ? '#22c55e' : '#94a3b8' }}>{conv.online ? '● متصل الآن' : conv.sub}</div>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <button style={{ width: 34, height: 34, borderRadius: 8, background: '#f0f5fb', border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Icon name="phone" size={15} stroke="#64748b" />
            </button>
            <button style={{ width: 34, height: 34, borderRadius: 8, background: '#f0f5fb', border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Icon name="eye" size={15} stroke="#64748b" />
            </button>
          </div>
        </div>

        {/* Messages */}
        <div style={{ flex: 1, overflowY: 'auto', padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: 10, background: '#f7fafd' }}>
          {msgs.map(m => {
            const isMe = m.from === 'me' || m.from === 'clinic';
            return (
              <div key={m.id} style={{ display: 'flex', flexDirection: 'column', alignItems: isMe ? 'flex-end' : 'flex-start' }}>
                {m.sender && !isMe && (
                  <div style={{ fontSize: 11, color: '#94a3b8', marginBottom: 3, marginLeft: 8 }}>{m.sender}</div>
                )}
                {m.sender && isMe && (
                  <div style={{ fontSize: 11, color: '#94a3b8', marginBottom: 3, marginRight: 8 }}>{m.sender}</div>
                )}
                <div style={{
                  maxWidth: '68%', padding: '10px 14px',
                  borderRadius: isMe ? '16px 4px 16px 16px' : '4px 16px 16px 16px',
                  background: isMe ? '#3d7ab5' : '#fff',
                  color: isMe ? '#fff' : '#0d2137',
                  boxShadow: '0 2px 8px rgba(0,0,0,0.06)',
                  fontSize: 13, lineHeight: 1.6,
                  border: isMe ? 'none' : '1px solid #e8f0f9',
                }}>
                  {m.text}
                </div>
                <div style={{ fontSize: 10, color: '#94a3b8', marginTop: 3 }}>
                  {m.time} · {m.date} {isMe && '✓✓'}
                </div>
              </div>
            );
          })}
          <div ref={bottomRef} />
        </div>

        {/* Input */}
        <div style={{ padding: '10px 16px', background: '#fff', borderTop: '1px solid #e8f0f9', display: 'flex', gap: 8, alignItems: 'flex-end', flexShrink: 0 }}>
          <button style={{ width: 36, height: 36, borderRadius: 99, background: '#f0f5fb', border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, fontSize: 16 }}>📎</button>
          <div style={{ flex: 1, background: '#f7fafd', borderRadius: 20, padding: '8px 14px', border: '1.5px solid #dce8f5' }}>
            <textarea
              value={input}
              onChange={e => setInput(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); sendMsg(); } }}
              placeholder="اكتب رسالتك هنا... (Enter للإرسال)"
              style={{ width: '100%', background: 'none', border: 'none', fontFamily: 'Tajawal', fontSize: 13, color: '#0d2137', resize: 'none', outline: 'none', maxHeight: 100, minHeight: 20 }}
              rows={1}
            />
          </div>
          <button onClick={sendMsg} disabled={!input.trim()} style={{
            width: 38, height: 38, borderRadius: 99,
            background: input.trim() ? '#3d7ab5' : '#e8f0f9',
            border: 'none', cursor: input.trim() ? 'pointer' : 'not-allowed',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            transition: 'all 0.15s', flexShrink: 0,
            boxShadow: input.trim() ? '0 3px 10px rgba(61,122,181,0.4)' : 'none',
          }}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke={input.trim() ? '#fff' : '#94a3b8'} strokeWidth="2.5" strokeLinecap="round"><line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/></svg>
          </button>
        </div>
      </div>
    );
  }

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '320px 1fr', height: '100%', background: '#fff', borderRadius: 12, overflow: 'hidden', boxShadow: '0 1px 3px rgba(13,33,55,0.06)', border: '1px solid #e8f0f9' }}>
      {/* Left: conversation list */}
      <div style={{ borderLeft: '1px solid #e8f0f9', padding: 16, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        <ConvList />
      </div>
      {/* Right: chat */}
      <ChatWindow />
    </div>
  );
}

Object.assign(window, { MessagingPage });
