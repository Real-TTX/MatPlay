// ============ MatPlay – Play-Core: API, Live-Polling, Wake Lock, Teilen ============

const MatPlayCore = (function () {
    const root = document.getElementById('playRoot');
    const token = root.dataset.token;
    const statusBadge = document.getElementById('statusBadge');

    let state = null;
    let lastVersion = -1;
    let renderFn = null;
    let pollTimer = null;

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
        get state() { return state; },
        init(render) {
            renderFn = render;
            refresh(true).then(startPolling);
        },
    };
})();
