// ============ MatPlay – Seite "Neues Spiel": Preset-Übernahme, Spielerliste, Vorauswahl ============

(function () {
    const form = document.getElementById('createForm');
    if (!form) return;
    const moduleSelect = document.getElementById('ModuleKey');
    const presetInput = document.getElementById('Preset');
    const presetPicker = presetInput.closest('.mat-picker');
    const PRESETS = JSON.parse(form.dataset.presets || '[]');

    function setChecked(id, value) {
        const el = document.getElementById(id);
        el.checked = value;
        el.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // Preset gewählt: Modul + Konfiguration (und ggf. Name) übernehmen, Spieler bleiben erhalten
    presetInput.addEventListener('change', () => {
        const preset = PRESETS.find(p => p.key === presetInput.value);
        if (!preset) return;
        moduleSelect.value = preset.moduleKey;
        moduleSelect.dispatchEvent(new Event('change', { bubbles: true }));
        const nameInput = document.getElementById('Name');
        if (!nameInput.value.trim() || PRESETS.some(p => p.name === nameInput.value)) {
            nameInput.value = preset.name;
        }
        const c = preset.config || {};
        if (preset.moduleKey === 'counter') {
            document.getElementById('StartScore').value = c.startScore ?? 0;
            document.getElementById('Step').value = c.step ?? 1;
            document.getElementById('TargetScore').value = c.targetScore ?? 0;
            setChecked('HasTarget', c.targetScore !== undefined && c.targetScore !== null);
            setChecked('LowestWins', !!c.lowestWins);
            setChecked('AllowNegative', c.allowNegative !== false);
            setChecked('UseRounds', !!c.useRounds);
            setChecked('SubtractRounds', !!c.subtractRounds);
        } else if (preset.moduleKey === 'qwixx') {
            document.getElementById('QwixxVariant').value = c.variant || 'classic';
        } else if (preset.moduleKey === 'munchkin') {
            setChecked('MunchkinTrackHealth', !!c.trackHealth);
        }
    });

    // Manueller Modulwechsel: das Preset passt nicht mehr
    moduleSelect.addEventListener('change', e => {
        if (!e.isTrusted) return;
        if (presetInput.value) MatUi.picker.set(presetPicker, '');
    });

    // Spielerliste
    const playerList = document.getElementById('playerList');
    function addPlayerRow(value) {
        const row = document.createElement('div');
        row.className = 'mp-player-row';
        row.innerHTML = '<input type="text" class="mat-input" name="Players" placeholder="Spielername" maxlength="40" list="savedPlayersList" aria-label="Spielername" />' +
            '<button type="button" class="mat-btn mat-btn--ghost mat-btn--icon remove-player" title="Entfernen" aria-label="Spieler entfernen">' +
            '<svg class="mat-icon" aria-hidden="true" focusable="false"><use href="#mat-i-x"></use></svg></button>';
        const input = row.querySelector('input');
        input.value = value || '';
        playerList.appendChild(row);
        return input;
    }
    document.getElementById('addPlayer').addEventListener('click', () => addPlayerRow().focus());
    playerList.addEventListener('click', e => {
        const remove = e.target.closest('.remove-player');
        if (!remove) return;
        remove.closest('.mp-player-row').remove();
        if (!playerList.children.length) addPlayerRow();
    });

    // Vorauswahl-Chips: gespeicherten Spieler in die nächste freie Zeile eintragen
    const chips = document.getElementById('savedPlayerChips');
    if (chips) {
        const names = () => [...playerList.querySelectorAll('input')].map(i => i.value.trim().toLowerCase());
        const syncChips = () => chips.querySelectorAll('.mp-chip').forEach(c => {
            const used = names().includes(c.dataset.player.toLowerCase());
            c.classList.toggle('is-used', used);
            c.disabled = used;
        });
        chips.addEventListener('click', e => {
            const chip = e.target.closest('.mp-chip');
            if (!chip || chip.disabled) return;
            const empty = [...playerList.querySelectorAll('input')].find(i => !i.value.trim());
            if (empty) empty.value = chip.dataset.player;
            else addPlayerRow(chip.dataset.player);
            syncChips();
        });
        playerList.addEventListener('input', syncChips);
        playerList.addEventListener('click', () => setTimeout(syncChips, 0));
        syncChips();
    }
})();
