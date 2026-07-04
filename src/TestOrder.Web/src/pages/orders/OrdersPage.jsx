import React, { useCallback, useEffect, useState } from 'react';
import { createOrder, fetchOrders, fetchProducts } from '../../api/api.js';
import PageNav from '../../components/PageNav.jsx';
import { DATE_PRESETS, getRecentRange } from '../../shared/dateRanges.js';
import { formatCurrency, formatDate } from '../../shared/formatters.js';

const PAGE_SIZE = 20;
const ORDER_STATUS_OPTIONS = ['created', 'processed'];

function summarizeItems(items) {
  return items.map((item) => `${item.productName} × ${item.quantity}`).join(', ');
}

export default function OrdersPage({ onSystemErrorChange }) {
  const [products, setProducts] = useState([]);
  const [loadingProducts, setLoadingProducts] = useState(true);
  const [productsError, setProductsError] = useState(null);

  const [orders, setOrders] = useState([]);
  const [pagination, setPagination] = useState({
    page: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 1,
  });
  const [page, setPage] = useState(1);
  const [refreshKey, setRefreshKey] = useState(0);
  const [loadingOrders, setLoadingOrders] = useState(true);
  const [ordersError, setOrdersError] = useState(null);

  // Rascunho dos filtros (o que aparece nos campos) vs. aplicado (o que realmente vai para a API) —
  // separados de propósito: digitar/duplo-clique nos campos não dispara busca, só o clique em Filtrar/Limpar filtros.
  const [statusFilterDraft, setStatusFilterDraft] = useState('');
  const [startDateFilterDraft, setStartDateFilterDraft] = useState('');
  const [endDateFilterDraft, setEndDateFilterDraft] = useState('');
  const [appliedStatus, setAppliedStatus] = useState('');
  const [appliedStartDate, setAppliedStartDate] = useState('');
  const [appliedEndDate, setAppliedEndDate] = useState('');
  const [ordersFilterError, setOrdersFilterError] = useState(null);

  const [draftOrder, setDraftOrder] = useState({ customerName: '', items: [] });
  const [selectedProductId, setSelectedProductId] = useState('');
  const [quantityInput, setQuantityInput] = useState('');
  const [itemError, setItemError] = useState(null);

  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState(null);
  const [createSuccessMessage, setCreateSuccessMessage] = useState(null);

  useEffect(() => {
    onSystemErrorChange(Boolean(productsError || ordersError));
  }, [productsError, ordersError, onSystemErrorChange]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoadingProducts(true);
      setProductsError(null);
      try {
        const data = await fetchProducts();
        if (!cancelled) setProducts(data);
      } catch (err) {
        if (!cancelled) setProductsError(err.message);
      } finally {
        if (!cancelled) setLoadingProducts(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const loadOrders = useCallback(async (targetPage) => {
    setLoadingOrders(true);
    setOrdersError(null);
    try {
      const data = await fetchOrders(targetPage, PAGE_SIZE, {
        status: appliedStatus,
        startDate: appliedStartDate,
        endDate: appliedEndDate,
      });
      setOrders(data.items);
      setPagination({
        page: data.page,
        pageSize: data.pageSize,
        totalCount: data.totalCount,
        totalPages: data.totalPages,
      });
    } catch (err) {
      setOrdersError(err.message);
    } finally {
      setLoadingOrders(false);
    }
  }, [appliedStatus, appliedStartDate, appliedEndDate]);

  useEffect(() => {
    loadOrders(page);
  }, [page, refreshKey, loadOrders]);

  function handleRefresh() {
    setRefreshKey((key) => key + 1);
  }

  function handleGoToPage(targetPage) {
    const totalPages = Math.max(1, pagination.totalPages);
    const nextPage = Math.min(Math.max(1, targetPage), totalPages);
    if (nextPage !== page) setPage(nextPage);
  }

  function handleFilterOrders(event) {
    event.preventDefault();
    setOrdersFilterError(null);

    if (startDateFilterDraft && endDateFilterDraft && startDateFilterDraft > endDateFilterDraft) {
      setOrdersFilterError('A data inicial não pode ser posterior à data final.');
      return;
    }

    setAppliedStatus(statusFilterDraft);
    setAppliedStartDate(startDateFilterDraft);
    setAppliedEndDate(endDateFilterDraft);
    setPage(1);
  }

  function handleClearOrderFilters() {
    setStatusFilterDraft('');
    setStartDateFilterDraft('');
    setEndDateFilterDraft('');
    setAppliedStatus('');
    setAppliedStartDate('');
    setAppliedEndDate('');
    setOrdersFilterError(null);
    setPage(1);
  }

  function handleDatePreset(dayCount) {
    const range = getRecentRange(dayCount);
    setStartDateFilterDraft(range.startDate);
    setEndDateFilterDraft(range.endDate);
    setOrdersFilterError(null);
  }

  function handleAddItem(event) {
    event.preventDefault();
    setItemError(null);

    if (!selectedProductId) {
      setItemError('Selecione um produto.');
      return;
    }

    const trimmedQuantity = quantityInput.trim();
    const isNumeric = /^-?\d+$/.test(trimmedQuantity);
    const quantity = Number(trimmedQuantity);

    if (trimmedQuantity === '' || !isNumeric || !Number.isInteger(quantity) || quantity <= 0) {
      setItemError('Informe uma quantidade inteira maior que zero.');
      return;
    }

    const productId = Number(selectedProductId);
    if (draftOrder.items.some((item) => item.productId === productId)) {
      setItemError('Este produto já foi adicionado — remova-o para alterar a quantidade.');
      return;
    }

    const product = products.find((p) => p.id === productId);
    if (!product) {
      setItemError('Produto inválido.');
      return;
    }

    setDraftOrder((prev) => ({
      ...prev,
      items: [
        ...prev.items,
        {
          productId: product.id,
          productName: product.name,
          unitPrice: product.unitPrice,
          quantity,
        },
      ],
    }));
    setQuantityInput('');
  }

  function handleRemoveItem(productId) {
    setDraftOrder((prev) => ({
      ...prev,
      items: prev.items.filter((item) => item.productId !== productId),
    }));
  }

  async function handleCreateOrder(event) {
    event.preventDefault();
    setCreateError(null);
    setCreateSuccessMessage(null);
    setItemError(null);

    if (draftOrder.items.length === 0) {
      setCreateError('Adicione ao menos um item.');
      return;
    }

    setCreating(true);
    try {
      const payload = {
        customerName: draftOrder.customerName || null,
        items: draftOrder.items.map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
        })),
      };
      const created = await createOrder(payload);
      setDraftOrder({ customerName: '', items: [] });
      setSelectedProductId('');
      setQuantityInput('');
      setCreateSuccessMessage(`Pedido #${created.id} criado com sucesso.`);
      setPage(1);
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setCreateError(err.message);
    } finally {
      setCreating(false);
    }
  }

  return (
    <main className="app-main">
      <section className="panel order-form-panel">
        <h2>Novo pedido</h2>

        <form className="order-form" onSubmit={handleCreateOrder}>
          <div className="form-row">
            <label htmlFor="customerName">Nome do cliente (opcional)</label>
            <input
              id="customerName"
              type="text"
              value={draftOrder.customerName}
              onChange={(e) =>
                setDraftOrder((prev) => ({ ...prev, customerName: e.target.value }))
              }
              placeholder="Ex.: Maria Silva"
            />
          </div>

          {loadingProducts && <p className="hint">Carregando produtos...</p>}
          {productsError && <p className="error-message">{productsError}</p>}

          {!loadingProducts && !productsError && (
            <div className="form-row item-row">
              <div className="item-field">
                <label htmlFor="productSelect">Produto</label>
                <select
                  id="productSelect"
                  value={selectedProductId}
                  onChange={(e) => setSelectedProductId(e.target.value)}
                >
                  <option value="">Selecione...</option>
                  {products.map((product) => (
                    <option key={product.id} value={product.id}>
                      {product.name} — {formatCurrency(product.unitPrice)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="item-field item-field-quantity">
                <label htmlFor="quantityInput">Quantidade</label>
                <input
                  id="quantityInput"
                  type="text"
                  inputMode="numeric"
                  value={quantityInput}
                  onChange={(e) => setQuantityInput(e.target.value)}
                  placeholder="1"
                />
              </div>
              <button type="button" className="btn-secondary" onClick={handleAddItem}>
                Adicionar item
              </button>
            </div>
          )}

          {itemError && <p className="error-message">{itemError}</p>}

          <ul className="draft-items">
            {draftOrder.items.length === 0 && (
              <li className="draft-items-empty">Nenhum item adicionado ainda.</li>
            )}
            {draftOrder.items.map((item) => (
              <li key={item.productId} className="draft-item">
                <span>
                  {item.productName} × {item.quantity} (
                  {formatCurrency(item.unitPrice * item.quantity)})
                </span>
                <button
                  type="button"
                  className="btn-link"
                  onClick={() => handleRemoveItem(item.productId)}
                >
                  remover
                </button>
              </li>
            ))}
          </ul>

          {createError && <p className="error-message">{createError}</p>}
          {createSuccessMessage && <p className="success-message">{createSuccessMessage}</p>}

          <button type="submit" className="btn-primary" disabled={creating}>
            {creating ? 'Criando...' : 'Criar pedido'}
          </button>
        </form>
      </section>

      <section className="panel orders-panel">
        <div className="orders-panel-header">
          <div className="orders-panel-title">
            <h2>Pedidos</h2>
            <div className="stat-chips">
              <span className="stat-chip">{pagination.totalCount} pedidos</span>
              <span className="stat-chip">
                pág. {pagination.page}/{pagination.totalPages}
              </span>
            </div>
          </div>
          <button type="button" className="btn-secondary" onClick={handleRefresh}>
            Atualizar
          </button>
        </div>

        <form className="orders-filter-form" onSubmit={handleFilterOrders}>
          <div className="form-row">
            <label htmlFor="orderStatusFilter">Status</label>
            <select
              id="orderStatusFilter"
              value={statusFilterDraft}
              onChange={(e) => setStatusFilterDraft(e.target.value)}
              onDoubleClick={() => setStatusFilterDraft('')}
            >
              <option value="">Todos</option>
              {ORDER_STATUS_OPTIONS.map((status) => (
                <option key={status} value={status}>
                  {status}
                </option>
              ))}
            </select>
          </div>
          <div className="form-row">
            <label htmlFor="orderStartDateFilter">Data inicial</label>
            <input
              id="orderStartDateFilter"
              type="date"
              value={startDateFilterDraft}
              onChange={(e) => setStartDateFilterDraft(e.target.value)}
              onDoubleClick={() => setStartDateFilterDraft('')}
            />
          </div>
          <div className="form-row">
            <label htmlFor="orderEndDateFilter">Data final</label>
            <input
              id="orderEndDateFilter"
              type="date"
              value={endDateFilterDraft}
              onChange={(e) => setEndDateFilterDraft(e.target.value)}
              onDoubleClick={() => setEndDateFilterDraft('')}
            />
          </div>
          <button type="submit" className="btn-primary">
            Filtrar
          </button>
          <button type="button" className="btn-link" onClick={handleClearOrderFilters}>
            Limpar filtros
          </button>
        </form>

        <div className="date-presets" aria-label="Atalhos de período">
          {DATE_PRESETS.map((preset) => (
            <button
              key={preset.label}
              type="button"
              className="btn-secondary date-preset-button"
              onClick={() => handleDatePreset(preset.dayCount)}
            >
              {preset.label}
            </button>
          ))}
        </div>

        {ordersFilterError && <p className="error-message">{ordersFilterError}</p>}

        {loadingOrders && <p className="hint">Carregando pedidos...</p>}
        {ordersError && <p className="error-message">{ordersError}</p>}

        {!loadingOrders && !ordersError && orders.length === 0 && (
          <p className="hint">Nenhum pedido encontrado.</p>
        )}

        {!loadingOrders && !ordersError && orders.length > 0 && (
          <div className="orders-table-wrapper">
            <table className="orders-table">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Data</th>
                  <th>Cliente</th>
                  <th>Status</th>
                  <th>Total</th>
                  <th>Itens</th>
                </tr>
              </thead>
              <tbody>
                {orders.map((order) => (
                  <tr key={order.id}>
                    <td>{order.id}</td>
                    <td>{formatDate(order.createdAt)}</td>
                    <td>{order.customerName || '—'}</td>
                    <td>{order.status}</td>
                    <td>{formatCurrency(order.total)}</td>
                    <td>{summarizeItems(order.items)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <PageNav page={page} totalPages={pagination.totalPages} onGoToPage={handleGoToPage} />
      </section>
    </main>
  );
}
