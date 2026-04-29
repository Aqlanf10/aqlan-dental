import type { Config } from "tailwindcss";

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
        /* ─── shadcn/ui base tokens (HSL CSS variables) ─── */
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
        popover: {
          DEFAULT: "hsl(var(--popover))",
          foreground: "hsl(var(--popover-foreground))",
        },

        /* ─── Aqlan Dental Pro Design System ─── */

        // Primary palette
        "primary-dark": "#0a1c30",
        "primary-light": "#1a3a5c",

        // Accent / Interactive
        "accent-blue": "#3d7ab5",
        "blue-hover": "#2d5e8e",

        // Orange accent
        orange: {
          DEFAULT: "#f5922e",
          hover: "#e07d1e",
        },

        // Green accent
        green: {
          DEFAULT: "#22c55e",
          dark: "#16a34a",
        },

        // Red accent
        red: {
          DEFAULT: "#ef4444",
          hover: "#dc2626",
        },

        // Specialty colors
        purple: "#a855f7",
        yellow: "#f59e0b",
        sky: "#0ea5e9",

        // Background tones
        "bg-main": "#eef3f9",
        "bg-card": "#ffffff",
        "bg-sidebar": "#0d2137",
        "bg-topbar": "#ffffff",
        "bg-subtle": "#f7fafd",
        "bg-input": "#f7fafd",
        "bg-light-blue": "#f0f5fb",

        // Border tones
        "border-primary": "#e8f0f9",
        "border-light": "#f1f5f9",
        "border-input": "#dce8f5",

        // Text tones
        "text-primary": "#0d2137",
        "text-secondary": "#64748b",
        "text-muted": "#94a3b8",
        "text-on-dark": "#ffffff",

        // Legacy clinic tokens (kept for backward compat)
        clinic: {
          teal: "#0E7490",
          gold: "#D97706",
          "teal-light": "#ECFEFF",
          "gold-light": "#FFFBEB",
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
        modal: "0 20px 60px rgba(13,33,55,0.2)",
        "button-blue": "0 4px 12px rgba(61,122,181,0.3)",
      },
      keyframes: {
        fadeIn: {
          "0%": { opacity: "0", transform: "translateY(6px)" },
          "100%": { opacity: "1", transform: "translateY(0)" },
        },
        fadeUp: {
          "0%": { opacity: "0", transform: "translateY(12px)" },
          "100%": { opacity: "1", transform: "translateY(0)" },
        },
        slideInRTL: {
          from: { transform: "translateX(100%)", opacity: "0" },
          to: { transform: "translateX(0)", opacity: "1" },
        },
        hide: {
          from: { opacity: "1" },
          to: { opacity: "0" },
        },
      },
      animation: {
        "fade-in": "fadeIn 0.2s ease-out",
        "fade-up": "fadeUp 0.3s ease-out",
        "slide-in": "slideInRTL 0.2s ease-out",
        hide: "hide 0.2s ease-in",
      },
    },
  },
  plugins: [],
};

export default config;
