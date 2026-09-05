// ============ MatPlay – Modul: Qwixx (klassisch + gemixxt-Varianten) ============

(function () {
    const tabs = document.getElementById('qwixxTabs');
    const pad = document.getElementById('qwixxPad');
    const scoresTable = document.getElementById('qwixxScores');

    let activePlayerId = null;

    function range(from, to) {
        const list = [];
        const step = from <= to ? 1 : -1;
        for (let i = from; i !== to + step; i += step) list.push(i);
        return list;
    }

    function classicRows() {
        return [
            { key: 'red', color: 'red', cells: range(2, 12).map(n => ({ n, color: 'red' })) },
            { key: 'yellow', color: 'yellow', cells: range(2, 12).map(n => ({ n, color: 'yellow' })) },
            { key: 'green', color: 'green', cells: range(12, 2).map(n => ({ n, color: 'green' })) },
            { key: 'blue', color: 'blue', cells: range(12, 2).map(n => ({ n, color: 'blue' })) },
        ];
    }

    function gameRows(state) {
        const rows = state.config && state.config.rows;
        if (!rows || !rows.length) return classicRows();
        return rows.map(r => ({ key: r.key, color: r.color || null, cells: r.cells }));
    }

    function playerState(rows, player) {
        const s = player.state || {};
        const result = { misses: s.misses || 0 };
        for (const row of rows) result[row.key] = s[row.key] || [];
        return result;
    }

    function rowScore(count) { return count * (count + 1) / 2; }

    function totalScore(rows, ps) {
        let total = 0;
        for (const row of rows) {
            let crosses = ps[row.key].length;
            const last = row.cells[row.cells.length - 1].n;
            if (ps[row.key].includes(last)) crosses += 1; // Schloss zählt als Extra-Kreuz
            total += rowScore(crosses);
        }
        return total - ps.misses * 5;
    }

    function lockedRows(state) {
        return (state.config && state.config.lockedRows) || [];
    }

    function render(state) {
        if (!state.players.length) { pad.innerHTML = '<p class="form-hint">Noch keine Spieler.</p>'; return; }
        if (!state.players.some(p => p.id === activePlayerId)) {
            const pinned = state.players.find(p => MatPlayCore.isPinned(p.id));
            activePlayerId = (pinned ?? state.players[0]).id;
        }
        const rows = gameRows(state);

        // Spieler-Tabs
        tabs.innerHTML = '';
        for (const player of state.players) {
            const tab = document.createElement('button');
            tab.type = 'button';
            tab.className = 'tab' + (player.id === activePlayerId ? ' active' : '');
            tab.textContent = `${player.name} · ${totalScore(rows, playerState(rows, player))}`;
            tab.addEventListener('click', () => { activePlayerId = player.id; render(state); });
            tabs.appendChild(tab);
        }

        const player = state.players.find(p => p.id === activePlayerId);
        // Bearbeiten nur für an diesem Gerät gepinnte Spieler (ohne Pins: alle)
        const running = state.status === 0 && MatPlayCore.editable(player.id);
        const ps = playerState(rows, player);
        const locked = lockedRows(state);

        pad.innerHTML = '';
        for (const row of rows) {
            const rowEl = document.createElement('div');
            rowEl.className = 'qwixx-row ' + (row.color || 'mixed');
            const checked = ps[row.key];
            const rowLocked = locked.includes(row.key);
            const numbers = row.cells.map(c => c.n);
            const last = numbers[numbers.length - 1];

            row.cells.forEach((cellDef, idx) => {
                const cell = document.createElement('button');
                cell.type = 'button';
                cell.className = 'qwixx-cell c-' + cellDef.color;
                cell.textContent = cellDef.n;
                const isChecked = checked.includes(cellDef.n);
                if (isChecked) cell.classList.add('checked');

                const rightmostIdx = Math.max(-1, ...checked.map(n => numbers.indexOf(n)));
                const isLast = idx === numbers.length - 1;
                const canCheck = running && !isChecked && idx > rightmostIdx &&
                    !rowLocked && (!isLast || checked.length >= 5);
                const canUncheck = running && isChecked && idx === rightmostIdx;

                if (!canCheck && !canUncheck) cell.classList.add('disabled');
                cell.addEventListener('click', () => {
                    if (canCheck) toggle(state, rows, player, row, cellDef.n, true);
                    else if (canUncheck) toggle(state, rows, player, row, cellDef.n, false);
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
            box.className = 'qwixx-cell' + (ps.misses >= i ? ' checked miss' : '');
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
        total.textContent = `Punkte: ${totalScore(rows, ps)}`;
        pad.appendChild(total);

        renderScoreboard(state, rows);
    }

    async function toggle(state, rows, player, row, num, check) {
        const ps = playerState(rows, player);
        const list = ps[row.key];
        const next = { ...ps, [row.key]: check ? [...list, num] : list.filter(n => n !== num) };
        const numbers = row.cells.map(c => c.n);
        const last = numbers[numbers.length - 1];

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

    function renderScoreboard(state, rows) {
        const list = state.players
            .map(p => ({ name: p.name, score: totalScore(rows, playerState(rows, p)) }))
            .sort((a, b) => b.score - a.score);
        let html = '<thead><tr><th>#</th><th>Spieler</th><th>Punkte</th></tr></thead><tbody>';
        list.forEach((r, i) => {
            html += `<tr><td>${i === 0 ? '🏆' : i + 1}</td><td>${escapeHtml(r.name)}</td><td>${r.score}</td></tr>`;
        });
        scoresTable.innerHTML = html + '</tbody>';
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    MatPlayCore.init(render);
})();
