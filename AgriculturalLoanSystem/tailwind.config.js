/** @type {import('tailwindcss').Config} */
module.exports = {
  // (موديل 6، بند 6.1): يمسح كل ملفات Razor لاستخراج أصناف Tailwind المستخدمة فعليًا
  // ويبني ملف CSS واحد يحتوي عليها فقط (بدل تحميل محرك Tailwind كاملًا وقت التشغيل عبر CDN).
  content: [
    "./Views/**/*.cshtml",
    "./Pages/**/*.cshtml",
    "./wwwroot/**/*.html",
  ],
  theme: {
    extend: {},
  },
  plugins: [],
}
