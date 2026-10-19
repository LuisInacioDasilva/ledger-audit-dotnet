const byId = (id) => document.getElementById(id);
const date = (value) => new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
const number = (value) => new Intl.NumberFormat('pt-BR').format(value);

function row(tag, className, value) {
  const cell = document.createElement(tag);
  cell.className = className;
  cell.textContent = value;
  return cell;
}

async function refresh() {
  try {
    const [entriesResponse, balancesResponse] = await Promise.all([fetch('/api/entries'), fetch('/api/balances')]);
    if (!entriesResponse.ok || !balancesResponse.ok) throw new Error('API indisponível');
    const [entries, balances] = await Promise.all([entriesResponse.json(), balancesResponse.json()]);
    const accounts = Object.entries(balances);
    byId('eventCount').textContent = number(entries.length);
    byId('accountCount').textContent = number(accounts.length);
    const total = accounts.reduce((sum, [, amount]) => sum + amount, 0);
    byId('integrity').textContent = total === 0 ? 'BALANCED' : 'DIVERGENTE';
    byId('integrity').classList.toggle('bad', total !== 0);
    const body = byId('entryRows'); body.replaceChildren();
    if (entries.length === 0) {
      const tr = document.createElement('tr'); const td = row('td', 'empty', 'Ainda sem lançamentos. Use o exemplo de POST no README para movimentar o ledger.'); td.colSpan = 5; tr.append(td); body.append(tr);
    } else {
      for (const entry of entries.slice(0, 50)) {
        const tr = document.createElement('tr');
        tr.append(row('td', 'mono', entry.id), row('td', '', date(entry.occurredAt)), row('td', '', entry.description), row('td', 'mono', `${entry.lines.length} linhas`), row('td', 'good', '✓ BALANCED'));
        body.append(tr);
      }
    }
    const grid = byId('balanceRows'); grid.replaceChildren();
    if (accounts.length === 0) grid.append(row('div', 'empty', 'Nenhuma conta movimentada.'));
    for (const [account, amount] of accounts) {
      const item = document.createElement('article'); item.className = 'balance-card';
      item.append(row('span', '', account), row('strong', amount >= 0 ? '' : 'negative', number(amount)), row('small', '', 'MINOR UNITS'));
      grid.append(item);
    }
    byId('sync').textContent = `ATUALIZADO ${new Intl.DateTimeFormat('pt-BR', { timeStyle: 'medium' }).format(new Date())}`;
  } catch { byId('sync').textContent = 'FALHA AO SINCRONIZAR'; }
}

function tick() { byId('clock').textContent = new Intl.DateTimeFormat('pt-BR', { timeStyle: 'medium' }).format(new Date()); }
tick(); refresh(); setInterval(tick, 1000); setInterval(refresh, 15000);
