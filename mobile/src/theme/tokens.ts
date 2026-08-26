export const colors = {
  navy950: '#071733',
  navy900: '#0D2453',
  navy800: '#163967',
  navy700: '#1D4C7A',
  orange600: '#EE7D11',
  orange500: '#FF9317',
  orange100: '#FFF0DC',
  blue100: '#EAF2FA',
  blue50: '#F4F7FB',
  white: '#FFFFFF',
  ink: '#13233B',
  muted: '#64748B',
  border: '#D9E2EC',
  success: '#12715B',
  successSoft: '#E5F6F0',
  danger: '#B42318',
  dangerSoft: '#FDECEA',
  shadow: '#071733',
} as const;

export const spacing = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  xxl: 24,
  xxxl: 32,
} as const;

export const radius = {
  sm: 10,
  md: 14,
  lg: 20,
  xl: 28,
  pill: 999,
} as const;

export const typography = {
  title: { fontSize: 30, lineHeight: 38, fontWeight: '800' as const },
  heading: { fontSize: 21, lineHeight: 29, fontWeight: '800' as const },
  subheading: { fontSize: 17, lineHeight: 25, fontWeight: '700' as const },
  body: { fontSize: 15, lineHeight: 23, fontWeight: '400' as const },
  label: { fontSize: 14, lineHeight: 20, fontWeight: '700' as const },
  caption: { fontSize: 12, lineHeight: 18, fontWeight: '600' as const },
} as const;
