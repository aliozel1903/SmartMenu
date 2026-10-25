// Herkese açık sayfalar (ana sayfa, müşteri menüsü, giriş/kayıt sayfaları, hata sayfası) için
// varsayılan Tailwind ayarları. Çıktı: wwwroot/css/tailwind-site.css
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Views/Home/**/*.cshtml',
    './Views/Menu/Index.cshtml',
    './Views/Account/**/*.cshtml',
    './Views/Shared/Error.cshtml',
    './Controllers/**/*.cs',
    './ViewModels/**/*.cs'
  ],
  theme: {
    extend: {}
  },
  plugins: []
};
