import type { Config } from "tailwindcss";

/**
 * Aqlan Dental Pro — Tailwind Theme
 *
 * Brand Identity (مركز د. عقلان الكامل):
 *   - Primary Navy:  #0d2137  (sidebar bg, primary text, headings, main brand color)
 *   - Navy Darker:  #0a1c30  (login gradient start)
 *   - Navy Mid:      #1a3a5c  (sidebar collapse, gradient mid, secondary dark surface)
 *   - Brand Blue:    #3d7ab5  (primary action color, buttons, links, active nav, badges)
 *   - Brand Orange:  #f5922e  (secondary accent, CTA)
 *
 * Removed (not from brand identity):
 *   ✗ teal #0E7490 (off-brand)
 *   ✗ gold #D97706 (duplicates orange)
 */

const config: Config = {
  darkMode: ["class"],
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ["var(--font-tajawal)", "sans-serif"],
      },
      colors: {
        border: "hsl(var(--border))",
        input: "hsl(var(--input))",
        ring: "hsl(var(--ring))",
        background: "hsl(var(--background))",
        foreground: "hsl(var(--foreground))",
        primary: {
          DEFAULT: "hsl(var(--primary))",
          foreground: "hsl(var(--primary-foreground))",
        },
        secondary: {
          DEFAULT: "hsl(var(--secondary))",
          foreground: "hsl(var(--secondary-foreground))",
        },
        destructive: {
          DEFAULT: "hsl(var(--destructive))",
          foreground: "hsl(var(--destructive-foreground))",
        },
        muted: {
          DEFAULT: "hsl(var(--muted))",
          foreground: "hsl(var(--muted-foreground))",
        },
        accent: {
          DEFAULT: "hsl(var(--accent))",
          foreground: "hsl(var(--accent-foreground))",
        },
        card: {
          DEFAULT: "hsl(var(--card))",
          foreground: "hsl(var(--card-foreground))",
        },
        clinic: {
          /* ── Brand primary (dark sky) ─────────────────────────── */
          navy:         "#0d2137",
          "navy-dark":   "#0a1c30",
          "navy-mid":    "#1a3a5c",
          "navy-700":   "#244b73",
          "navy-600":   "#2e4a6f",
          "navy-500":   "#3d5e85",

          /* ── Sky blue accent ──────────────────────────────────── */
          blue:         "#3d7ab5",
          "blue-600":   "#2d5e8e",
          "blue-500":   "#5a94c9",
          "blue-100":   "#dce8f5",
          "blue-50":    "#f0f5fb",
          "blue-light": "#dce8f5",

          /* ── Brand orange (CTA) ───────────────────────────────── */
          orange:         "#f5922e",
          "orange-600":   "#e07d1e",
          "orange-500":   "#f7a44f",
          "orange-100":   "#fde8d0",
          "orange-50":    "#fff7ed",
          "orange-light": "#fde8d0",
        },
      },
      borderRadius: {
        lg: "var(--radius)",
        md: "calc(var(--radius) - 2px)",
        sm: "calc(var(--radius) - 4px)",
      },
      boxShadow: {
        card: "0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)",
        "card-hover": "0 6px 24px rgba(13,33,55,0.1)",
        dropdown: "0 8px 30px rgba(0,0,0,0.12)",
      },
    },
  },
  plugins: [],
};

export default config;
