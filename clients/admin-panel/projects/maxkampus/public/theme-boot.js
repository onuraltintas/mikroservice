// Applies the saved colour theme before Angular starts, so a dark-mode page never flashes light (T1, 31.10.2026).
// The `theme` key holds 'light', 'dark' or 'system'. Without one the page is light (27.11.2026): the device setting is
// followed only when the visitor chose 'system'. DarkModeService takes over after start-up. A same-origin file (not an
// inline script) so a strict Content-Security-Policy keeps it running.
(function () {
  try {
    var saved = localStorage.getItem('theme');
    var dark = saved === 'dark' || (saved === 'system' && window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
    document.documentElement.classList.toggle('dark', dark);
    document.documentElement.style.colorScheme = dark ? 'dark' : 'light';
  } catch (e) { /* storage blocked: keep the light default */ }
})();
