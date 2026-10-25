// Yönetim paneli (Views/Shared/_Layout.cshtml ve onu kullanan sayfalar) için Tailwind ayarları.
// Eskiden _Layout içinde CDN'e verilen ayarların aynısı; çıktı: wwwroot/css/tailwind-panel.css
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Views/**/*.cshtml',
    './wwwroot/js/**/*.js',
    './Controllers/**/*.cs',
    './ViewModels/**/*.cs',
    './Services/**/*.cs',
    './Models/**/*.cs'
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        'primary': '#0058bc',
        'primary-container': '#0070eb',
        'background': '#f5f5f7',
        'surface': '#fcf8fb',
        'on-surface': '#1b1b1d',
        'on-surface-variant': '#414755',
        'surface-container-lowest': '#ffffff',
        'surface-container': '#f0edef',
        'outline-variant': '#c1c6d7',
        'error': '#ba1a1a'
      },
      borderRadius: {
        'DEFAULT': '0.25rem',
        'lg': '0.5rem',
        'xl': '0.75rem',
        'card': '20px'
      }
    }
  },
  plugins: [
    require('@tailwindcss/forms'),
    require('@tailwindcss/container-queries')
  ]
};
