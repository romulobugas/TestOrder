const currencyFormatter = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
});

export function formatCurrency(value) {
  return currencyFormatter.format(value);
}

export function formatDate(isoString) {
  return new Date(isoString).toLocaleString('pt-BR', {
    timeZone: 'UTC',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

// Evita `new Date('YYYY-MM-DD')`: em alguns fusos (ex.: UTC-3) isso desloca o dia exibido,
// pois o construtor interpreta a string como meia-noite UTC. Split simples preserva o
// mesmo dia calendário retornado pelo backend.
export function formatCalendarDate(yyyyMmDd) {
  const [year, month, day] = yyyyMmDd.split('-');
  return `${day}/${month}/${year}`;
}
