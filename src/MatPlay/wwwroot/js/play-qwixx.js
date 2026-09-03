// ============ MatPlay – Modul: Qwixx ============

(function () {
    const tabs = document.getElementById('qwixxTabs');
    const pad = document.getElementById('qwixxPad');
    const scoresTable = document.getElementById('qwixxScores');

    const ROWS = [
        { key: 'red', label: 'Rot', numbers: range(2, 12) },
        { key: 'yellow', label: 'Gelb', numbers: range(2, 12) },
        { key: 'green', label: 'Grün', numbers: range(12, 2) },
        { key: 'blue', label: 'Blau', numbers: range(12, 2) },
    ];

    let activePlayerId = null;

    function range(from, to) {
        const list = [];
        const step = from <= to ? 1 : -1;
        for (let i = from; i !== to + step; i += step) list.push(i);
        return list;
    }

    function playerState(player) {
        const s = player.state || {};
        return {
            red: s.red || [], yellow: s.yellow || [],
            green: s.green || [], blue: s.blue || [],
            misses: s.misses || 0,
        };
    }

    function rowScore(count) { return count * (count + 1) / 2; }

    function totalScore(state) {
        let total = 0;
        for (const row of ROWS) {
            let crosses = state[row.key].length;
            const last = row.numbers[row.numbers.length - 1];
            if (state[row.key].includes(last)) crosses += 1; // Schloss zählt als Extra-Kreuz
            total += rowScore(crosses);
        }
        return total - state.misses * 5;
    }

    function lockedRows(gameState) {
        return (gameState.config && gameState.config.lockedRows) || [];
    }

    function render(state) {
        if (!state.players.length) { pad.innerHTML = '<p class="form-hint">Noch keine Spieler.</p>'; return; }
        if (!state.players.some(p => p.id === activePlayerId)) activePlayerId = state.players[0].id;
        const running = state.status === 0;

        // Spieler-Tabs
        tabs.innerHTML = '';
        for (const player of state.players) {
            const tab = document.createElement('button');
            tab.type = 'button';
            tab.className = 'tab' + (player.id === activePlayerId ? ' active' : '');
            tab.textContent = `${player.name} · ${totalScore(playerState(player))}`;
            tab.addEventListener('click', () => { activePlayerId = player.id; render(state); });
            tabs.appendChild(tab);
        }

        const player = state.players.find(p => p.id === activePlayerId);
        const ps = playerState(player);
        const locked = lockedRows(state);

        pad.innerHTML = '';
        for (const row of ROWS) {
            const rowEl = document.createElement('div');
            rowEl.className = 'qwixx-row ' + row.key;
            const checked = ps[row.key];
            const rowLocked = locked.includes(row.key);
            const last = row.numbers[row.numbers.length - 1];

            row.numbers.forEach((num, idx) => {
                const cell = document.createElement('button');
                cell.type = 'button';
                cell.className = 'qwixx-cell';
                cell.textContent = num;
                const isChecked = checked.includes(num);
                if (isChecked) cell.classList.add('checked');

                const rightmostIdx = Math.max(-1, ...checked.map(n => row.numbers.indexOf(n)));
                const isLast = num === last;
                const canCheck = running && !isChecked && idx > rightmostIdx &&
                    !rowLocked && (!isLast || checked.length >= 5);
                const canUncheck = running && isChecked && idx === rightmostIdx;

                if (!canCheck && !canUncheck) cell.classList.add('disabled');
                cell.addEventListener('click', () => {
                    if (canCheck) toggle(state, player, row, num, true);
                    else if (canUncheck) toggle(state, player, row, num, false);
                });
                rowEl.appendChild(cell);
            });

            const lockCell = document.createElement('span');
            lockCell.className = 'qwixx-cell lock' + (rowLocked || checked.includes(last) ? ' checked' : '');
            lockCell.textContent = '🔒';
            lockCell.title = rowLocked ? 'Reihe geschlossen' : 'Schloss – letzte Zahl braucht 5 Kreuze';
            rowEl.appendChild(lockCell);

            pad.appendChild(rowEl);
        }

        // Fehlwürfe
        const missRow = document.createElement('div');
        missRow.className = 'qwixx-misses';
        const label = document.createElement('span');
        label.className = 'label';
        label.textContent = 'Fehlwürfe (−5):';
        missRow.appendChild(label);
        for (let i = 1; i <= 4; i++) {
            const box = document.createElement('button');
            box.type = 'button';
            box.className = 'qwixx-cell' + (ps.misses >= i ? ' checked' : '');
            box.textContent = ps.misses >= i ? '✖' : '';
            box.addEventListener('click', () => {
                if (!running) return;
                const next = { ...ps, misses: ps.misses >= i ? i - 1 : i };
                MatPlayCore.action('/player-state', { playerId: player.id, state: next });
            });
            missRow.appendChild(box);
        }
        pad.appendChild(missRow);

        const total = document.createElement('div');
        total.className = 'qwixx-total';
        total.textContent = `Punkte: ${totalScore(ps)}`;
        pad.appendChild(total);

        renderScoreboard(state);
    }

    async function toggle(state, player, row, num, check) {
        const ps = playerState(player);
        const list = ps[row.key];
        const next = { ...ps, [row.key]: check ? [...list, num] : list.filter(n => n !== num) };
        const last = row.numbers[row.numbers.length - 1];

        await MatPlayCore.api('/player-state', { playerId: player.id, state: next });

        // Letzte Zahl angekreuzt/entfernt → Reihe global (ent)sperren
        if (num === last) {
            const locked = lockedRows(state).filter(k => k !== row.key);
            if (check) locked.push(row.key);
            const config = { ...state.config, lockedRows: locked };
            await MatPlayCore.api('/config', { config });
        }
        await MatPlayCore.refresh(true);
    }

    function renderScoreboard(state) {
        const rows = state.players
            .map(p => ({ name: p.name, score: totalScore(playerState(p)) }))
            .sort((a, b) => b.score - a.score);
        let html = '<thead><tr><th>#</th><th>Spieler</th><th>Punkte</th></tr></thead><tbody>';
        rows.forEach((r, i) => {
            html += `<tr><td>${i === 0 ? '🏆' : i + 1}</td><td>${escapeHtml(r.name)}</td><td>${r.score}</td></tr>`;
        });
        scoresTable.innerHTML = html + '</tbody>';
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    document.getElementById('addPlayerBtn').addEventListener('click', () => {
        const name = prompt('Name des neuen Spielers:');
        if (name && name.trim()) MatPlayCore.action('/player', { name: name.trim() });
    });

    MatPlayCore.init(render);
})();
