import React from 'react';
import { buildPageItems } from '../shared/pagination.js';

// Apresentação pura — mesmo markup/classes usados desde o módulo 004 para Pedidos,
// reaproveitado aqui para a tabela de dias do Faturamento (Início/Anterior/números/Próxima/Fim).
export default function PageNav({ page, totalPages, onGoToPage }) {
  const safeTotalPages = Math.max(1, totalPages);
  const pageItems = buildPageItems(page, safeTotalPages);

  return (
    <div className="pagination">
      <button type="button" onClick={() => onGoToPage(1)} disabled={page === 1}>
        Início
      </button>
      <button type="button" onClick={() => onGoToPage(page - 1)} disabled={page === 1}>
        Anterior
      </button>
      <div className="pagination-pages" aria-label="Páginas">
        {pageItems.map((item) =>
          item.type === 'ellipsis' ? (
            <span key={item.key} className="pagination-ellipsis">
              ...
            </span>
          ) : (
            <button
              key={item.pageNumber}
              type="button"
              className={item.pageNumber === page ? 'pagination-page pagination-page--active' : 'pagination-page'}
              aria-current={item.pageNumber === page ? 'page' : undefined}
              onClick={() => onGoToPage(item.pageNumber)}
            >
              {item.pageNumber}
            </button>
          ),
        )}
      </div>
      <button type="button" onClick={() => onGoToPage(page + 1)} disabled={page === safeTotalPages}>
        Próxima
      </button>
      <button type="button" onClick={() => onGoToPage(safeTotalPages)} disabled={page === safeTotalPages}>
        Fim
      </button>
    </div>
  );
}
