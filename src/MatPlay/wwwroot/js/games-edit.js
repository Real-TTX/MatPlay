// ============ MatPlay – Seite "Spiel bearbeiten": neue Spielerzeile ============

(function () {
    const addButton = document.getElementById('addPlayerRow');
    const list = document.getElementById('editPlayerList');
    if (!addButton || !list) return;

    addButton.addEventListener('click', () => {
        const row = document.createElement('div');
        row.className = 'mp-player-row';
        row.innerHTML = '<input type="hidden" name="PlayerIds" value="0" />' +
            '<input type="text" class="mat-input" name="PlayerNames" placeholder="Neuer Spieler" maxlength="40" aria-label="Neuer Spieler" />';
        list.appendChild(row);
        row.querySelector('input[type=text]').focus();
    });
})();
