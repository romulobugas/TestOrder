function padDatePart(value) {
  return String(value).padStart(2, '0');
}

export function formatLocalDate(date) {
  return `${date.getFullYear()}-${padDatePart(date.getMonth() + 1)}-${padDatePart(date.getDate())}`;
}

function addLocalDays(date, days) {
  const copy = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  copy.setDate(copy.getDate() + days);
  return copy;
}

// Datas locais (não string do backend) — usa componentes de data local, não `new Date(string)`.
export function getDefaultRevenueRange() {
  const now = new Date();
  return {
    startDate: `${now.getFullYear()}-${padDatePart(now.getMonth() + 1)}-01`,
    endDate: formatLocalDate(now),
  };
}

export function getRecentRange(dayCount) {
  const end = new Date();
  const start = addLocalDays(end, -(dayCount - 1));
  return {
    startDate: formatLocalDate(start),
    endDate: formatLocalDate(end),
  };
}

// Atalhos compartilhados entre filtros de Pedidos e consulta de Faturamento.
export const DATE_PRESETS = [
  { label: 'Hoje', dayCount: 1 },
  { label: '7 dias', dayCount: 7 },
  { label: '15 dias', dayCount: 15 },
  { label: '30 dias', dayCount: 30 },
  { label: '90 dias', dayCount: 90 },
  { label: 'Último ano', dayCount: 366 },
];
