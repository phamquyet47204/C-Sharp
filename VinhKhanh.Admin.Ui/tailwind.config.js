/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        coral: {
          50: '#fff1ec',
          100: '#ffe4d3',
          200: '#ffcaaf',
          300: '#ffaa82',
          400: '#ff7f50', // Base Coral
          500: '#fc5a25',
          600: '#ea3e0c',
          700: '#c32c0d',
          800: '#9b2512',
          900: '#7d2213',
          950: '#440f07',
        }
      },
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
      }
    },
  },
  plugins: [],
}
