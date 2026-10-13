// ============ MatPlay – Modul: Phase 10 (Phasen-Tracker + Minuspunkte) ============

(function () {
    const board = document.getElementById('phase10Board');
    const history = document.getElementById('phase10History');
    const table = document.getElementById('phase10Table');
    const { icon, escapeHtml } = MatPlayCore;

    const PHASES = [
        'Zwei Drillinge',
        'Ein Drilling + eine Viererfolge',
        'Ein Vierling + eine Viererfolge',
        'Eine Siebenerfolge',
        'Eine Achterfolge',
        'Eine Neunerfolge',
        'Zwei Vierlinge',
        'Sieben Karten einer Farbe',
        'Ein Fünfling + ein Drilling',
        'Ein Fünfling + ein Vierling',
    ];

    function playerRounds(player) {
        return (player.state && player.state.rounds) || [];
    }

    // Aktuelle Phase = 1 + geschaffte Runden; > 10 bedeutet fertig
    function currentPhase(player) {
        return 1 + playerRounds(player).filter(r => r.done).length;
    }

    function totalPoints(player) {
        return playerRounds(player).reduce((sum, r) => sum + r.points, 0);
    }

    function textEl(tag, className, text) {
        const node = document.createElement(tag);
        node.className = className;
        node.textContent = text;
        return node;
    }

    function render(state) {
        if (!state.players.length) { board.innerHTML = '<p class="mat-muted">Noch keine Spieler.</p>'; return; }
        const running = state.status === 0;

        // Gewinner: Phase 10 geschafft; bei mehreren die wenigsten Punkte
        const finished = state.players.filter(p => currentPhase(p) > 10);
        let winnerIds = new Set();
        if (finished.length) {
            const min = Math.min(...finished.map(totalPoints));
            winnerIds = new Set(finished.filter(p => totalPoints(p) === min).map(p => p.id));
        }
        // Führt: höchste Phase, bei Gleichstand wenigste Punkte
        const bestPhase = Math.max(...state.players.map(currentPhase));
        const bestPoints = Math.min(...state.players.filter(p => currentPhase(p) === bestPhase).map(totalPoints));

        board.innerHTML = '';
        const players = [...state.players].sort((a, b) =>
            Number(MatPlayCore.isPinned(b.id)) - Number(MatPlayCore.isPinned(a.id)));
        for (const player of players) {
            const rounds = playerRounds(player);
            const phase = currentPhase(player);
            const points = totalPoints(player);
            const canEdit = MatPlayCore.editable(player.id);
            const winner = winnerIds.has(player.id);
            const leader = !winner && winnerIds.size === 0 && state.players.length > 1 && rounds.length > 0
                && phase === bestPhase && points === bestPoints;

            const card = document.createElement('div');
            card.className = 'mp-player' + (winner ? ' is-winner' : leader ? ' is-leader' : '')
                + (canEdit ? '' : ' is-readonly');

            card.appendChild(textEl('div', 'mp-player__name', player.name));
            card.appendChild(textEl('div', 'mp-player__tag', winner ? 'Gewonnen!' : leader ? 'Führt' : ''));
            card.appendChild(textEl('div', 'mp-phase', phase > 10 ? 'Alle Phasen geschafft' : `Phase ${phase}`));
            card.appendChild(textEl('div', 'mp-phase-desc', phase <= 10 ? PHASES[phase - 1] : ''));
            const pointsEl = textEl('div', 'mp-total mp-total--md', String(points));
            pointsEl.title = 'Minuspunkte gesamt';
            card.appendChild(pointsEl);

            if (running && canEdit && phase <= 10) {
                card.appendChild(textEl('div', 'mp-round-tag', `Runde ${rounds.length + 1}`));

                const doneLabel = document.createElement('label');
                doneLabel.className = 'mat-check';
                const done = document.createElement('input');
                done.type = 'checkbox';
                doneLabel.append(done, textEl('span', 'mat-check__text', 'Phase geschafft'));
                card.appendChild(doneLabel);

                const form = document.createElement('div');
                form.className = 'mp-entry';
                const input = document.createElement('input');
                input.type = 'number';
                input.className = 'mat-input';
                input.inputMode = 'numeric';
                input.min = 0;
                input.placeholder = 'Minuspunkte';
                input.setAttribute('aria-label', `Punkte für ${player.name}`);
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'mat-btn mat-btn--primary mat-btn--icon';
                btn.innerHTML = icon('check');
                btn.setAttribute('aria-label', 'Runde eintragen');
                const submit = () => {
                    const value = parseInt(input.value, 10);
                    if (Number.isNaN(value) || value < 0) return;
                    MatPlayCore.action('/player-state', {
                        playerId: player.id,
                        state: { rounds: [...rounds, { points: value, done: done.checked }] },
                    });
                };
                btn.addEventListener('click', submit);
                input.addEventListener('keydown', e => { if (e.key === 'Enter') submit(); });
                form.append(input, btn);
                card.appendChild(form);
            }

            if (running && canEdit && rounds.length > 0) {
                const undo = document.createElement('button');
                undo.type = 'button';
                undo.className = 'mat-btn mat-btn--ghost mat-btn--sm';
                undo.innerHTML = icon('restore') + '<span>Letzte Runde</span>';
                undo.addEventListener('click', () => {
                    MatPlayCore.action('/player-state', { playerId: player.id, state: { rounds: rounds.slice(0, -1) } });
                });
                card.appendChild(undo);
            }
            board.appendChild(card);
        }

        renderHistory(state);
    }

    function renderHistory(state) {
        const played = Math.max(...state.players.map(p => playerRounds(p).length));
        if (played === 0) { history.hidden = true; return; }
        history.hidden = false;

        let html = '<thead><tr><th scope="col">Runde</th>' +
            state.players.map(p => `<th scope="col" class="mat-num">${escapeHtml(p.name)}</th>`).join('') + '</tr></thead><tbody>';
        for (let r = 0; r < played; r++) {
            html += `<tr><td data-label="Runde">${r + 1}</td>`;
            for (const p of state.players) {
                const entry = playerRounds(p)[r];
                if (!entry) { html += `<td data-label="${escapeHtml(p.name)}" class="mat-num">–</td>`; continue; }
                html += `<td data-label="${escapeHtml(p.name)}" class="mat-num">${entry.points} ` +
                    `<span class="${entry.done ? 'mp-pos' : 'mp-dim'}">${entry.done ? 'Phase geschafft' : 'nicht geschafft'}</span></td>`;
            }
            html += '</tr>';
        }
        table.innerHTML = html + '</tbody>';
    }

    MatPlayCore.init(render);
})();
