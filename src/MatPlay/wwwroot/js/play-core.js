// ============ MatPlay – Play-Core: API, Live-Polling, Pins, Wake Lock, Teilen ============

const MatPlayCore = (function () {
    const root = document.getElementById('playRoot');
    const token = root.dataset.token;
    const statusBadge = document.getElementById('statusBadge');

    let state = null;
    let lastVersion = -1;
    let renderFn = null;
    let pollTimer = null;

    /** Setzt nur das Textlabel eines Buttons (das Icon bleibt erhalten). */
    function setLabel(btn, text) {
        const label = btn.querySelector('.mp-lbl');
        if (label) label.textContent = text;
    }

    // ---- Spieler-Pinning: dieses Gerät bedient nur die gepinnten Spieler (lokal gespeichert) ----
    const PIN_KEY = 'matplay-pins-' + token;
    let pins = [];
    try { pins = JSON.parse(localStorage.getItem(PIN_KEY) || '[]'); } catch { pins = []; }

    function isPinned(playerId) { return pins.includes(playerId); }
    function editable(playerId) { return pins.length === 0 || pins.includes(playerId); }

    let pinBarSignature = '';

    function renderPinBar() {
        const bar = document.getElementById('pinBar');
        if (!bar || !state) return;
        if (state.players.length < 2) { bar.hidden = true; return; }
        bar.hidden = false;
        // Verwaiste Pins entfernen (Spieler gelöscht)
        pins = pins.filter(id => state.players.some(p => p.id === id));

        // Die Leiste nur neu bauen, wenn sich die Spielerliste ändert (ein offenes Panel bleibt sonst stehen)
        const signature = state.players.map(p => p.id + ':' + p.name).join(',');
        if (signature !== pinBarSignature) {
            buildPinBar(bar);
            pinBarSignature = signature;
        }
    }

    /** "Dieses Gerät bedient": Multi-Select-Picker des Toolkits (mobil als Vollbild-Dialog). */
    function buildPinBar(bar) {
        bar.innerHTML = '';
        const label = document.createElement('label');
        label.className = 'mat-label mp-pin__label';
        label.htmlFor = 'pinValue';
        label.textContent = 'Dieses Gerät bedient';

        const wrapper = document.createElement('div');
        wrapper.className = 'mat-picker';
        wrapper.setAttribute('data-picker', 'select');
        wrapper.setAttribute('data-multiple', '');
        wrapper.setAttribute('data-clearable', '');
        wrapper.setAttribute('data-placeholder', 'Alle Spieler');

        const input = document.createElement('input');
        input.className = 'mat-picker__input';
        input.id = 'pinValue';
        input.value = pins.join(',');

        const options = document.createElement('div');
        options.className = 'mat-picker__options';
        options.hidden = true;
        for (const player of state.players) {
            const option = document.createElement('button');
            option.type = 'button';
            option.className = 'mat-picker__option';
            option.dataset.value = String(player.id);
            option.innerHTML = '<span class="mat-picker__text"><span class="mat-picker__name"></span></span>';
            option.querySelector('.mat-picker__name').textContent = player.name;
            options.appendChild(option);
        }

        wrapper.append(input, options);
        bar.append(label, wrapper);
        MatUi.picker.init(bar);

        input.addEventListener('change', () => {
            pins = input.value
                ? input.value.split(',').map(Number).filter(n => !Number.isNaN(n))
                : [];
            localStorage.setItem(PIN_KEY, JSON.stringify(pins));
            if (renderFn && state) renderFn(state);
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
        statusBadge.textContent = running ? 'Läuft' : 'Beendet';
        statusBadge.className = 'mat-pill ' + (running ? 'mat-pill--ok' : 'mat-pill--plain');
        const finishBtn = document.getElementById('finishBtn');
        if (finishBtn) setLabel(finishBtn, running ? 'Beenden' : 'Fortsetzen');
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

    // ---- Kurzregeln (Hilfe-Button) ----
    (function () {
        const btn = document.getElementById('rulesBtn');
        const panel = document.getElementById('rulesPanel');
        if (!btn || !panel) return;
        btn.addEventListener('click', () => {
            panel.hidden = !panel.hidden;
            btn.setAttribute('aria-expanded', String(!panel.hidden));
        });
    })();

    // ---- Wake Lock (Bildschirm anlassen, iOS 16.4+/Android) ----
    (function () {
        const btn = document.getElementById('wakeLockBtn');
        if (!btn) return;
        if (!('wakeLock' in navigator)) { btn.hidden = true; return; }
        let lock = null;

        async function acquire() {
            try {
                lock = await navigator.wakeLock.request('screen');
                btn.classList.add('is-active');
                setLabel(btn, 'Display bleibt an');
                lock.addEventListener('release', () => {
                    lock = null;
                    btn.classList.remove('is-active');
                    setLabel(btn, 'Display an');
                });
            } catch { /* z.B. Energiesparmodus */ }
        }

        btn.addEventListener('click', () => lock ? lock.release() : acquire());
        document.addEventListener('visibilitychange', () => {
            // Wake Lock wird beim Tab-Wechsel freigegeben – aktiv gewesenen Lock wiederholen
            if (!document.hidden && btn.classList.contains('is-active') && !lock) acquire();
        });
    })();

    // ---- Spiel beenden / fortsetzen (Button nur für den Besitzer gerendert) ----
    (function () {
        const btn = document.getElementById('finishBtn');
        if (!btn) return;
        btn.addEventListener('click', async () => {
            if (!state) return;
            const running = state.status === 0;
            const ok = await MatUi.confirm(
                running ? 'Spiel beenden? Danach kann nicht mehr gezählt werden.' : 'Spiel fortsetzen?',
                { ok: running ? 'Beenden' : 'Fortsetzen', danger: running });
            if (!ok) return;
            await action('/status', { status: running ? 1 : 0 });
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
                MatUi.toast('Link kopiert', { tone: 'success' });
            }
        });
    })();

    /** Ein Icon aus dem Sprite als SVG-Markup (für per Skript gebaute Oberflächen). */
    function icon(name) {
        return '<svg class="mat-icon" aria-hidden="true" focusable="false"><use href="#mat-i-' + name + '"></use></svg>';
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    return {
        token,
        api,
        action,
        refresh,
        isPinned,
        editable,
        icon,
        escapeHtml,
        get state() { return state; },
        init(render) {
            renderFn = render;
            refresh(true).then(startPolling);
        },
    };
})();
