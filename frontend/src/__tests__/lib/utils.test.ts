import { describe, it, expect } from 'vitest';
import { normalizePhone, formatPhoneForWhatsApp, formatTime, localDateString } from '@/lib/utils';
import {
  buildAnnouncementText,
  formatRoomForSpeech,
  formatFileNumberForSpeech,
} from '@/lib/clinic-display-announcement';

// ─── normalizePhone ──────────────────────────────────────────────────────────

describe('normalizePhone', () => {
  it('normalizes a 9-digit number starting with 7', () => {
    expect(normalizePhone('770245745')).toBe('967770245745');
  });

  it('strips leading 0 from 10-digit local number', () => {
    expect(normalizePhone('0770245745')).toBe('967770245745');
  });

  it('strips + prefix', () => {
    expect(normalizePhone('+967770245745')).toBe('967770245745');
  });

  it('strips 00 international prefix', () => {
    expect(normalizePhone('00967770245745')).toBe('967770245745');
  });

  it('converts Arabic-Indic digits to Western', () => {
    expect(normalizePhone('٧٧٠٢٤٥٧٤٥')).toBe('967770245745');
  });

  it('returns empty string for falsy input', () => {
    expect(normalizePhone('')).toBe('');
  });
});

// ─── formatPhoneForWhatsApp ──────────────────────────────────────────────────

describe('formatPhoneForWhatsApp', () => {
  it('returns empty string for null/undefined', () => {
    expect(formatPhoneForWhatsApp(null)).toBe('');
    expect(formatPhoneForWhatsApp(undefined)).toBe('');
  });

  it('normalizes phone for WhatsApp', () => {
    expect(formatPhoneForWhatsApp('0770245745')).toBe('967770245745');
  });
});

// ─── formatTime ──────────────────────────────────────────────────────────────

describe('formatTime', () => {
  it('formats AM times correctly', () => {
    expect(formatTime('09:30')).toBe('9:30 ص');
  });

  it('formats PM times correctly', () => {
    expect(formatTime('14:00')).toBe('2:00 م');
  });

  it('handles midnight (12 AM)', () => {
    expect(formatTime('00:15')).toBe('12:15 ص');
  });

  it('handles noon (12 PM)', () => {
    expect(formatTime('12:00')).toBe('12:00 م');
  });
});

// ─── formatRoomForSpeech ─────────────────────────────────────────────────────

describe('formatRoomForSpeech', () => {
  it('extracts number from room name', () => {
    expect(formatRoomForSpeech('غرفة 1')).toBe('الغرفة رقم 1');
  });

  it('handles Arabic-Indic digits', () => {
    expect(formatRoomForSpeech('غرفة ٢')).toBe('الغرفة رقم ٢');
  });

  it('handles standalone number', () => {
    expect(formatRoomForSpeech('3')).toBe('الغرفة رقم 3');
  });

  it('falls back to الاستقبال for empty string', () => {
    expect(formatRoomForSpeech('')).toBe('الاستقبال');
  });

  it('falls back to الاستقبال for null/undefined', () => {
    expect(formatRoomForSpeech(null)).toBe('الاستقبال');
    expect(formatRoomForSpeech(undefined)).toBe('الاستقبال');
  });

  it('returns text as-is when no number', () => {
    expect(formatRoomForSpeech('الاستقبال')).toBe('الاستقبال');
  });
});

// ─── formatFileNumberForSpeech ───────────────────────────────────────────────

describe('formatFileNumberForSpeech', () => {
  it('converts digits to Arabic words', () => {
    expect(formatFileNumberForSpeech('622')).toBe('ستة اثنين اثنين');
  });

  it('handles hyphenated file numbers', () => {
    expect(formatFileNumberForSpeech('2020-622')).toBe(
      'اثنين صفر اثنين صفر ستة اثنين اثنين'
    );
  });

  it('handles file numbers with English letters', () => {
    expect(formatFileNumberForSpeech('GM2026-0022')).toBe(
      'جي إم اثنين صفر اثنين ستة صفر صفر اثنين اثنين'
    );
  });

  it('returns empty string for empty/null/undefined input', () => {
    expect(formatFileNumberForSpeech('')).toBe('');
    expect(formatFileNumberForSpeech(null)).toBe('');
    expect(formatFileNumberForSpeech(undefined)).toBe('');
  });

  it('handles single digit', () => {
    expect(formatFileNumberForSpeech('5')).toBe('خمسة');
  });

  it('skips separators (dash, space, slash)', () => {
    expect(formatFileNumberForSpeech('1-2 3/4')).toBe(
      'واحد اثنين ثلاثة أربعة'
    );
  });
});

// ─── buildAnnouncementText ───────────────────────────────────────────────────

describe('buildAnnouncementText', () => {
  it('builds full announcement with name, number, and room', () => {
    const result = buildAnnouncementText('علي أحمد', '2020-622', 'غرفة 1');
    expect(result).toContain('المراجع علي أحمد');
    expect(result).toContain('اثنين صفر اثنين صفر ستة اثنين اثنين');
    expect(result).toContain('الغرفة رقم 1');
    expect(result).toContain('يرجى التوجه إلى');
  });

  it('handles empty patient name', () => {
    const result = buildAnnouncementText('', '2020-622', 'غرفة 2');
    expect(result).toContain('المراجع،');
    expect(result).not.toContain('المراجع ');
    expect(result).toContain('الغرفة رقم 2');
  });

  it('falls back to الاستقبال when room is empty', () => {
    const result = buildAnnouncementText('علي أحمد', '2020-622', '');
    expect(result).toContain('الاستقبال');
  });

  it('handles file numbers with letters', () => {
    const result = buildAnnouncementText('سارة محمد', 'GM2026-0022', 'غرفة 1');
    expect(result).toContain('جي إم');
    expect(result).toContain('المراجع سارة محمد');
  });

  it('handles empty patient number with fallback', () => {
    const result = buildAnnouncementText('أحمد', '', 'غرفة 3');
    expect(result).toContain('غير محدد');
  });
});

// ─── localDateString ─────────────────────────────────────────────────────────

describe('localDateString', () => {
  it('formats a date as YYYY-MM-DD with zero padding', () => {
    expect(localDateString(new Date(2026, 0, 5))).toBe('2026-01-05');
    expect(localDateString(new Date(2026, 11, 31))).toBe('2026-12-31');
  });

  it('uses the LOCAL date even late at night (UTC would roll to tomorrow)', () => {
    // 23:30 local on June 12 — toISOString() in UTC+3 would yield June 13.
    const lateNight = new Date(2026, 5, 12, 23, 30, 0);
    expect(localDateString(lateNight)).toBe('2026-06-12');
  });

  it('handles local first-of-month without shifting to previous month', () => {
    // Regression for finance OverviewTab: new Date(y, m, 1) at local midnight
    // converted via toISOString() shifted to the previous month's last day in UTC+3.
    const firstOfMonth = new Date(2026, 5, 1, 0, 0, 0);
    expect(localDateString(firstOfMonth)).toBe('2026-06-01');
  });

  it('defaults to the current local date', () => {
    const now = new Date();
    const expected = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
    expect(localDateString()).toBe(expected);
  });
});
