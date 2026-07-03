import { useCallback, useEffect, useState } from 'react';
import { fetchProducts, fetchOrders, createOrder } from './api.js';

const PAGE_SIZE = 20;

const currencyFormatter = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
});

function formatDate(isoString) {
  return new Date(isoString).toLocaleString('pt-BR', {
    timeZone: 'UTC',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function summarizeItems(items) {
  return items.map((item) => `${item.productName} × ${item.quantity}`).join(', ');
}

export default function App() {
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

  const [draftOrder, setDraftOrder] = useState({ customerName: '', items: [] });
  const [selectedProductId, setSelectedProductId] = useState('');
  const [quantityInput, setQuantityInput] = useState('');
  const [itemError, setItemError] = useState(null);

  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState(null);
  const [createSuccessMessage, setCreateSuccessMessage] = useState(null);

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
      const data = await fetchOrders(targetPage, PAGE_SIZE);
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
  }, []);

  useEffect(() => {
    loadOrders(page);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, refreshKey]);

  function handleRefresh() {
    setRefreshKey((key) => key + 1);
  }

  function handlePreviousPage() {
    if (page > 1) setPage((current) => current - 1);
  }

  function handleNextPage() {
    if (page < pagination.totalPages) setPage((current) => current + 1);
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

  const hasSystemError = Boolean(productsError || ordersError);

  return (
    <div className="app">
      <header className="app-header">
        <div className="app-header-title">
          <h1>TestOrder</h1>
          <p>Gestão de pedidos</p>
        </div>
        <span className={`status-badge ${hasSystemError ? 'status-badge--warning' : 'status-badge--online'}`}>
          <span className="status-dot" />
          {hasSystemError ? 'Instabilidade detectada' : 'Sistema operacional'}
        </span>
      </header>

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
                        {product.name} — {currencyFormatter.format(product.unitPrice)}
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
                    {currencyFormatter.format(item.unitPrice * item.quantity)})
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
                      <td>{order.status}</td>
                      <td>{currencyFormatter.format(order.total)}</td>
                      <td>{summarizeItems(order.items)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="pagination">
            <button type="button" onClick={handlePreviousPage} disabled={page === 1}>
              Anterior
            </button>
            <button
              type="button"
              onClick={handleNextPage}
              disabled={page === pagination.totalPages}
            >
              Próxima
            </button>
          </div>
        </section>
      </main>
    </div>
  );
}
