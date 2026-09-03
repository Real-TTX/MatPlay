// ============ MatPlay – Basis ============

// Theme-Umschalter (System / Hell / Dunkel)
(function () {
    const buttons = document.querySelectorAll('[data-theme-choice]');
    const media = matchMedia('(prefers-color-scheme: dark)');

    function apply(choice) {
        localStorage.setItem('matplay-theme', choice);
        const dark = choice === 'dark' || (choice === 'system' && media.matches);
        document.documentElement.dataset.theme = dark ? 'dark' : 'light';
        document.documentElement.dataset.themeChoice = choice;
        buttons.forEach(b => b.classList.toggle('active', b.dataset.themeChoice === choice));
    }

    buttons.forEach(b => b.addEventListener('click', () => apply(b.dataset.themeChoice)));
    media.addEventListener('change', () => apply(localStorage.getItem('matplay-theme') || 'system'));
    apply(localStorage.getItem('matplay-theme') || 'system');
})();

// Mobiles Menü
(function () {
    const app = document.querySelector('.app');
    const toggle = document.getElementById('menuToggle');
    const backdrop = document.getElementById('sidebarBackdrop');
    if (!toggle) return;
    toggle.addEventListener('click', () => app.classList.toggle('menu-open'));
    backdrop.addEventListener('click', () => app.classList.remove('menu-open'));
})();

// Kopieren-Buttons (data-copy="#selector")
document.addEventListener('click', async e => {
    const btn = e.target.closest('[data-copy]');
    if (!btn) return;
    const input = document.querySelector(btn.dataset.copy);
    if (!input) return;
    try {
        await navigator.clipboard.writeText(input.value);
        const old = btn.textContent;
        btn.textContent = '✔ Kopiert';
        setTimeout(() => btn.textContent = old, 1500);
    } catch {
        input.select();
        document.execCommand('copy');
    }
});

// Service Worker (PWA)
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/sw.js').catch(() => {});
}
