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
          navy:         "#0d2137",
          "navy-700":   "#1a3a5c",
          "navy-600":   "#243d5e",
          "navy-500":   "#2e4a6f",
          blue:         "#3d7ab5",
          "blue-500":   "#5a94c9",
          "blue-100":   "#dce8f5",
          "blue-50":    "#f0f5fb",
          "blue-light": "#dce8f5",
          orange:       "#f5922e",
          "orange-500": "#f7a44f",
          "orange-100": "#fde8d0",
          "orange-50":  "#fff7ed",
          "orange-light": "#fde8d0",
          teal:         "#0E7490",
          gold:         "#D97706",
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
      },
    },
  },
  plugins: [],
};

export default config;
