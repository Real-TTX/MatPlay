// ============ MatPlay – Modul: Phase 10 (Phasen-Tracker + Minuspunkte) ============

(function () {
    const board = document.getElementById('phase10Board');
    const history = document.getElementById('phase10History');
    const table = document.getElementById('phase10Table');

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

    function render(state) {
        if (!state.players.length) { board.innerHTML = '<p class="form-hint">Noch keine Spieler.</p>'; return; }
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
            card.className = 'counter-card' + (winner ? ' winner' : leader ? ' leader' : '')
                + (canEdit ? '' : ' readonly');

            const name = document.createElement('div');
            name.className = 'counter-name';
            name.textContent = player.name;
            card.appendChild(name);

            const tag = document.createElement('div');
            tag.className = 'counter-winner-tag';
            tag.textContent = winner ? '🏆 GEWONNEN!' : leader ? '⭐ Führt' : '';
            card.appendChild(tag);

            const phaseEl = document.createElement('div');
            phaseEl.className = 'phase-badge';
            phaseEl.textContent = phase > 10 ? '✔ Alle Phasen!' : `Phase ${phase}`;
            card.appendChild(phaseEl);

            if (phase <= 10) {
                const desc = document.createElement('div');
                desc.className = 'phase-desc';
                desc.textContent = PHASES[phase - 1];
                card.appendChild(desc);
            }

            const pointsEl = document.createElement('div');
            pointsEl.className = 'counter-total phase-points';
            pointsEl.textContent = points;
            pointsEl.title = 'Minuspunkte gesamt';
            card.appendChild(pointsEl);

            if (running && canEdit && phase <= 10) {
                const roundTag = document.createElement('div');
                roundTag.className = 'counter-round-tag';
                roundTag.textContent = `Runde ${rounds.length + 1}`;
                card.appendChild(roundTag);

                const doneLabel = document.createElement('label');
                doneLabel.className = 'form-check phase-done';
                const done = document.createElement('input');
                done.type = 'checkbox';
                const doneText = document.createElement('span');
                doneText.textContent = 'Phase geschafft';
                doneLabel.append(done, doneText);
                card.appendChild(doneLabel);

                const form = document.createElement('div');
                form.className = 'counter-round-form';
                const input = document.createElement('input');
                input.type = 'number';
                input.inputMode = 'numeric';
                input.min = 0;
                input.placeholder = 'Minuspunkte';
                input.setAttribute('aria-label', `Punkte für ${player.name}`);
                const btn = document.createElement('button');
                btn.className = 'btn btn-primary btn-sm';
                btn.textContent = '✔';
                btn.title = 'Runde eintragen';
                const submit = () => {
                    const value = parseInt(input.value, 10);
                    if (Number.isNaN(value) || value < 0) return;
                    const next = { rounds: [...rounds, { points: value, done: done.checked }] };
                    MatPlayCore.action('/player-state', { playerId: player.id, state: next });
                };
                btn.addEventListener('click', submit);
                input.addEventListener('keydown', e => { if (e.key === 'Enter') submit(); });
                form.append(input, btn);
                card.appendChild(form);
            }

            if (running && canEdit && rounds.length > 0) {
                const undo = document.createElement('button');
                undo.className = 'btn btn-ghost btn-sm';
                undo.textContent = '↩️ Letzte Runde';
                undo.addEventListener('click', () => {
                    MatPlayCore.action('/player-state', {
                        playerId: player.id,
                        state: { rounds: rounds.slice(0, -1) },
                    });
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

        let html = '<thead><tr><th>Runde</th>' +
            state.players.map(p => `<th>${escapeHtml(p.name)}</th>`).join('') + '</tr></thead><tbody>';
        for (let r = 0; r < played; r++) {
            html += `<tr><td>${r + 1}</td>`;
            for (const p of state.players) {
                const entry = playerRounds(p)[r];
                if (!entry) { html += '<td>–</td>'; continue; }
                html += `<td>${entry.points}` +
                    ` <span class="${entry.done ? 'log-value pos' : 'log-round'}">${entry.done ? '✔ Phase' : '✖'}</span></td>`;
            }
            html += '</tr>';
        }
        table.innerHTML = html + '</tbody>';
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    MatPlayCore.init(render);
})();
