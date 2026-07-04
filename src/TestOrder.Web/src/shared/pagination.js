// Compartilhado entre a paginação de Pedidos e a paginação de dias do Faturamento
// para manter o mesmo padrão visual: Início, Anterior, números, Próxima, Fim.
export function buildPageItems(currentPage, totalPages) {
  const safeTotal = Math.max(1, totalPages);
  const safeCurrent = Math.min(Math.max(1, currentPage), safeTotal);
  const visiblePages = new Set([1, safeTotal, safeCurrent, safeCurrent - 1, safeCurrent + 1]);

  if (safeCurrent <= 3) {
    visiblePages.add(2);
    visiblePages.add(3);
  }
  if (safeCurrent >= safeTotal - 2) {
    visiblePages.add(safeTotal - 1);
    visiblePages.add(safeTotal - 2);
  }

  const pages = Array.from(visiblePages)
    .filter((pageNumber) => pageNumber >= 1 && pageNumber <= safeTotal)
    .sort((a, b) => a - b);

  const items = [];
  pages.forEach((pageNumber, index) => {
    const previous = pages[index - 1];
    if (previous && pageNumber - previous > 1) {
      items.push({ type: 'ellipsis', key: `ellipsis-${previous}-${pageNumber}` });
    }
    items.push({ type: 'page', pageNumber });
  });

  return items;
}
