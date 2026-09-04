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

// Custom Select (durchsuchbares Dropdown, mobil als Dialog) – Markup: Controls/_SearchSelect
window.MPSelect = (function () {
    function init(root) {
        const trigger = root.querySelector('.mp-select-trigger');
        const search = root.querySelector('.mp-select-search');
        const hidden = root.querySelector('input[type=hidden]');
        const closeBtn = root.querySelector('.mp-select-close');
        const backdrop = root.querySelector('.mp-select-backdrop');
        const options = [...root.querySelectorAll('.mp-select-option')];

        function open() {
            root.classList.add('open');
            search.value = '';
            filter('');
            setTimeout(() => search.focus(), 60);
        }
        function close() { root.classList.remove('open'); }
        function filter(query) {
            query = query.trim().toLowerCase();
            options.forEach(o => o.hidden = query !== '' && !o.dataset.text.includes(query));
        }
        function set(value, silent) {
            hidden.value = value;
            options.forEach(o => o.classList.toggle('selected', o.dataset.value === value));
            const selected = options.find(o => o.dataset.value === value);
            root.querySelector('.mp-select-label').textContent = selected
                ? selected.querySelector('.mp-option-main').textContent.trim()
                : trigger.dataset.placeholder;
            if (!silent) hidden.dispatchEvent(new Event('change', { bubbles: true }));
        }

        trigger.addEventListener('click', () => root.classList.contains('open') ? close() : open());
        closeBtn.addEventListener('click', close);
        backdrop.addEventListener('click', close);
        search.addEventListener('input', () => filter(search.value));
        options.forEach(o => o.addEventListener('click', () => { set(o.dataset.value); close(); }));
        document.addEventListener('click', e => { if (!root.contains(e.target)) close(); });
        document.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });

        root.mpSet = value => set(value, true);
    }
    document.querySelectorAll('[data-mpselect]').forEach(init);
    return { init };
})();

// Service Worker (PWA)
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/sw.js').catch(() => {});
}
