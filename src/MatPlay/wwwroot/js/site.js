// ============ MatPlay – Seiten-Basis ============
// Theme, Menü, Dialoge, Kopieren-Buttons und Picker liefert das MatUi-Toolkit (matui.js).
// Hier bleibt nur, was die App selbst braucht: der Service Worker (PWA).

if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/sw.js').catch(() => {});
}
