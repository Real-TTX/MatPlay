// ============ MatPlay – Modul: Wizard (Ansage vs. Stiche, Auto-Scoring) ============

(function () {
    const info = document.getElementById('wizardInfo');
    const board = document.getElementById('wizardBoard');
    const history = document.getElementById('wizardHistory');
    const table = document.getElementById('wizardTable');

    function playerRounds(player) {
        return (player.state && player.state.rounds) || [];
    }

    function roundScore(entry) {
        return entry.bid === entry.tricks
            ? 20 + 10 * entry.tricks
            : -10 * Math.abs(entry.bid - entry.tricks);
    }

    function totalScore(player) {
        return playerRounds(player).reduce((sum, entry) => sum + roundScore(entry), 0);
    }

    function maxRounds(state) {
        return Math.max(1, Math.floor(60 / Math.max(state.players.length, 1)));
    }

    function render(state) {
        if (!state.players.length) { board.innerHTML = '<p class="form-hint">Noch keine Spieler.</p>'; return; }
        const running = state.status === 0;
        const max = maxRounds(state);
        const minRound = Math.min(...state.players.map(p => playerRounds(p).length));
        const finished = minRound >= max;

        info.textContent = finished
            ? `🏁 Alle ${max} Runden gespielt!`
            : `Runde ${minRound + 1} von ${max} · ${minRound + 1} Karte${minRound + 1 > 1 ? 'n' : ''} pro Spieler`;

        const totals = new Map(state.players.map(p => [p.id, totalScore(p)]));
        const best = Math.max(...totals.values());

        board.innerHTML = '';
        const players = [...state.players].sort((a, b) =>
            Number(MatPlayCore.isPinned(b.id)) - Number(MatPlayCore.isPinned(a.id)));
        for (const player of players) {
            const rounds = playerRounds(player);
            const total = totals.get(player.id);
            const canEdit = MatPlayCore.editable(player.id);
            const winner = finished && total === best;
            const leader = !finished && state.players.length > 1 && total === best && rounds.length > 0;

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

            const totalEl = document.createElement('div');
            totalEl.className = 'counter-total';
            totalEl.textContent = total;
            card.appendChild(totalEl);

            if (running && canEdit && rounds.length < max) {
                const roundTag = document.createElement('div');
                roundTag.className = 'counter-round-tag';
                roundTag.textContent = `Runde ${rounds.length + 1}`;
                card.appendChild(roundTag);

                const form = document.createElement('div');
                form.className = 'wizard-form';
                const bid = numberInput('Ansage', rounds.length + 1);
                const tricks = numberInput('Stiche', rounds.length + 1);
                const btn = document.createElement('button');
                btn.className = 'btn btn-primary btn-sm';
                btn.textContent = '✔';
                btn.title = 'Runde eintragen';
                const submit = () => {
                    const b = parseInt(bid.value, 10);
                    const t = parseInt(tricks.value, 10);
                    if (Number.isNaN(b) || Number.isNaN(t) || b < 0 || t < 0) return;
                    const next = { rounds: [...rounds, { bid: b, tricks: t }] };
                    MatPlayCore.action('/player-state', { playerId: player.id, state: next });
                };
                btn.addEventListener('click', submit);
                tricks.addEventListener('keydown', e => { if (e.key === 'Enter') submit(); });
                form.append(bid, tricks, btn);
                card.appendChild(form);
            }

            if (running && canEdit && rounds.length > 0) {
                const undo = document.createElement('button');
                undo.className = 'btn btn-ghost btn-sm';
                undo.textContent = '↩️ Letzte Runde';
                undo.title = 'Letzte Runde dieses Spielers zurücknehmen';
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

        renderHistory(state, max);
    }

    function numberInput(placeholder, maxValue) {
        const input = document.createElement('input');
        input.type = 'number';
        input.inputMode = 'numeric';
        input.min = 0;
        input.max = maxValue;
        input.placeholder = placeholder;
        input.setAttribute('aria-label', placeholder);
        return input;
    }

    function renderHistory(state, max) {
        const played = Math.max(...state.players.map(p => playerRounds(p).length));
        if (played === 0) { history.hidden = true; return; }
        history.hidden = false;

        let html = '<thead><tr><th>Runde</th>' +
            state.players.map(p => `<th>${escapeHtml(p.name)}</th>`).join('') + '</tr></thead><tbody>';
        for (let r = 0; r < Math.min(played, max); r++) {
            html += `<tr><td>${r + 1}</td>`;
            for (const p of state.players) {
                const entry = playerRounds(p)[r];
                if (!entry) { html += '<td>–</td>'; continue; }
                const score = roundScore(entry);
                const hit = entry.bid === entry.tricks;
                html += `<td><span class="${hit ? 'log-value pos' : 'log-value neg'}">${score > 0 ? '+' : ''}${score}</span>` +
                    ` <span class="log-round">${entry.tricks}/${entry.bid}</span></td>`;
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
