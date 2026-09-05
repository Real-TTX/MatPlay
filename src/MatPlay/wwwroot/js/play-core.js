// ============ MatPlay – Play-Core: API, Live-Polling, Wake Lock, Teilen ============

const MatPlayCore = (function () {
    const root = document.getElementById('playRoot');
    const token = root.dataset.token;
    const statusBadge = document.getElementById('statusBadge');

    let state = null;
    let lastVersion = -1;
    let renderFn = null;
    let pollTimer = null;

    // ---- Spieler-Pinning: dieses Gerät bedient nur die gepinnten Spieler (lokal gespeichert) ----
    const PIN_KEY = 'matplay-pins-' + token;
    let pins = [];
    try { pins = JSON.parse(localStorage.getItem(PIN_KEY) || '[]'); } catch { pins = []; }

    function isPinned(playerId) { return pins.includes(playerId); }
    function editable(playerId) { return pins.length === 0 || pins.includes(playerId); }

    // Pin-Auswahl als Multi-Select-Dropdown mit Checkboxen (mobil als Dialog, mp-select-Styles)
    let pinBarSignature = '';

    function renderPinBar() {
        const bar = document.getElementById('pinBar');
        if (!bar || !state) return;
        if (state.players.length < 2) { bar.hidden = true; return; }
        bar.hidden = false;
        // Verwaiste Pins entfernen (Spieler gelöscht)
        pins = pins.filter(id => state.players.some(p => p.id === id));

        const signature = state.players.map(p => p.id + ':' + p.name).join(',');
        if (signature !== pinBarSignature) {
            buildPinBar(bar);
            pinBarSignature = signature;
        }
        updatePinBar(bar);
    }

    function buildPinBar(bar) {
        const wasOpen = bar.querySelector('.mp-select')?.classList.contains('open');
        bar.innerHTML = '';

        const label = document.createElement('span');
        label.className = 'pin-label';
        label.textContent = '📌 Dieses Gerät bedient:';
        bar.appendChild(label);

        const root = document.createElement('div');
        root.className = 'mp-select pin-select' + (wasOpen ? ' open' : '');
        root.innerHTML =
            '<button type="button" class="mp-select-trigger" data-placeholder="Alle Spieler">' +
            '<span class="mp-select-label"></span><span class="mp-select-caret">▾</span></button>' +
            '<div class="mp-select-backdrop"></div>' +
            '<div class="mp-select-panel" role="dialog" aria-modal="true">' +
            '<div class="mp-select-head">' +
            '<input type="search" class="mp-select-search" placeholder="Spieler suchen …" aria-label="Spieler suchen" />' +
            '<button type="button" class="mp-select-close" aria-label="Schließen">✖</button></div>' +
            '<div class="mp-select-options"></div></div>';

        const optionsBox = root.querySelector('.mp-select-options');
        const makeOption = (playerId, text) => {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'mp-select-option pin-option';
            btn.dataset.pid = playerId === null ? '' : String(playerId);
            btn.dataset.text = text.toLowerCase();
            const check = document.createElement('span');
            check.className = 'pin-check';
            const main = document.createElement('span');
            main.className = 'mp-option-main';
            main.textContent = text;
            btn.append(check, main);
            btn.addEventListener('click', () => {
                if (playerId === null) pins = [];
                else pins = isPinned(playerId) ? pins.filter(id => id !== playerId) : [...pins, playerId];
                localStorage.setItem(PIN_KEY, JSON.stringify(pins));
                updatePinBar(bar);
                if (renderFn && state) renderFn(state);
            });
            return btn;
        };
        optionsBox.appendChild(makeOption(null, 'Alle Spieler'));
        for (const player of state.players) optionsBox.appendChild(makeOption(player.id, player.name));

        const trigger = root.querySelector('.mp-select-trigger');
        const search = root.querySelector('.mp-select-search');
        const filter = query => {
            query = query.trim().toLowerCase();
            optionsBox.querySelectorAll('.pin-option').forEach(option => {
                if (!option.dataset.pid) return; // "Alle Spieler" bleibt sichtbar
                option.hidden = query !== '' && !option.dataset.text.includes(query);
            });
        };
        const close = () => root.classList.remove('open');
        trigger.addEventListener('click', () => {
            if (root.classList.contains('open')) { close(); return; }
            root.classList.add('open');
            search.value = '';
            filter('');
            setTimeout(() => search.focus(), 60);
        });
        root.querySelector('.mp-select-close').addEventListener('click', close);
        root.querySelector('.mp-select-backdrop').addEventListener('click', close);
        search.addEventListener('input', () => filter(search.value));
        document.addEventListener('click', e => { if (!root.contains(e.target)) close(); });
        document.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });

        bar.appendChild(root);
    }

    function updatePinBar(bar) {
        const root = bar.querySelector('.mp-select');
        if (!root) return;
        const names = state.players.filter(p => isPinned(p.id)).map(p => p.name);
        root.querySelector('.mp-select-label').textContent = names.length ? names.join(', ') : 'Alle Spieler';
        root.querySelectorAll('.pin-option').forEach(option => {
            const pid = option.dataset.pid;
            const selected = pid === '' ? pins.length === 0 : isPinned(Number(pid));
            option.classList.toggle('selected', selected);
            option.querySelector('.pin-check').textContent = selected ? '☑' : '☐';
        });
    }

    async function api(path, body) {
        const res = await fetch(`/api/play/${token}${path}`, body === undefined
            ? undefined
            : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        if (!res.ok) throw new Error('API-Fehler ' + res.status);
        return res.json();
    }

    async function refresh(force) {
        try {
            const next = await api('/state');
            if (force || next.version !== lastVersion) {
                lastVersion = next.version;
                state = next;
                updateStatusBadge();
                renderPinBar();
                if (renderFn) renderFn(state);
            }
        } catch { /* offline o.ä. – nächster Poll versucht es erneut */ }
    }

    function updateStatusBadge() {
        if (!statusBadge || !state) return;
        const running = state.status === 0;
        statusBadge.textContent = running ? '🟢 Läuft' : '🏁 Beendet';
        statusBadge.className = 'badge ' + (running ? 'badge-live' : 'badge-done');
    }

    function startPolling() {
        stopPolling();
        pollTimer = setInterval(() => { if (!document.hidden) refresh(false); }, 2500);
    }
    function stopPolling() { if (pollTimer) clearInterval(pollTimer); }

    document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(false); });

    // Nach eigener Aktion sofort neu laden
    async function action(path, body) {
        await api(path, body ?? {});
        await refresh(true);
    }

    // ---- Wake Lock (Bildschirm anlassen, iOS 16.4+/Android) ----
    (function () {
        const btn = document.getElementById('wakeLockBtn');
        if (!btn) return;
        if (!('wakeLock' in navigator)) { btn.style.display = 'none'; return; }
        let lock = null;

        async function acquire() {
            try {
                lock = await navigator.wakeLock.request('screen');
                btn.classList.add('active');
                btn.textContent = '📱 Display bleibt an';
                lock.addEventListener('release', () => {
                    lock = null;
                    btn.classList.remove('active');
                    btn.textContent = '📱 Display an';
                });
            } catch { /* z.B. Energiesparmodus */ }
        }

        btn.addEventListener('click', () => lock ? lock.release() : acquire());
        document.addEventListener('visibilitychange', () => {
            // Wake Lock wird beim Tab-Wechsel freigegeben – aktiv gewesenen Lock wiederholen
            if (!document.hidden && btn.classList.contains('active') && !lock) acquire();
        });
    })();

    // ---- Teilen ----
    (function () {
        const btn = document.getElementById('shareBtn');
        if (!btn) return;
        btn.addEventListener('click', async () => {
            const url = location.href;
            if (navigator.share) {
                try { await navigator.share({ title: document.title, url }); } catch { /* abgebrochen */ }
            } else {
                await navigator.clipboard.writeText(url);
                const old = btn.textContent;
                btn.textContent = '✔ Link kopiert';
                setTimeout(() => btn.textContent = old, 1500);
            }
        });
    })();

    return {
        token,
        api,
        action,
        refresh,
        isPinned,
        editable,
        get state() { return state; },
        init(render) {
            renderFn = render;
            refresh(true).then(startPolling);
        },
    };
})();
