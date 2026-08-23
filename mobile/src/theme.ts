export const colors = {
  background: "#f0f5fb",
  surface: "#ffffff",
  surfaceMuted: "#f7f9fc",
  primary: "#1a3a5c",
  primaryLight: "#244b73",
  primarySoft: "#e8f0f8",
  secondary: "#3d7ab5",
  secondarySoft: "#eaf3fb",
  accent: "#f5922e",
  accentDark: "#d97618",
  accentSoft: "#fff2e4",
  text: "#15283d",
  muted: "#68788a",
  border: "#dbe5ef",
  borderStrong: "#c6d4e2",
  danger: "#b42318",
  dangerSoft: "#feeceb",
  success: "#16794c",
  successSoft: "#e7f7ef",
  warning: "#a15c07",
  warningSoft: "#fff5dc",
  white: "#ffffff",
  overlay: "rgba(10, 35, 59, 0.56)"
} as const;

export const spacing = {
  xxs: 4,
  xs: 8,
  sm: 12,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 44
} as const;

export const radius = {
  sm: 12,
  md: 18,
  lg: 24,
  xl: 30,
  pill: 999
} as const;

export const shadow = {
  card: {
    shadowColor: "#102a43",
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: 0.08,
    shadowRadius: 16,
    elevation: 3
  },
  floating: {
    shadowColor: "#102a43",
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.16,
    shadowRadius: 22,
    elevation: 7
  }
} as const;
