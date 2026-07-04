import React, { useState } from 'react';
import OrdersPage from './pages/orders/OrdersPage.jsx';
import RevenuePage from './pages/revenue/RevenuePage.jsx';

export default function App() {
  const [activeTab, setActiveTab] = useState('orders');
  const [systemError, setSystemError] = useState(false);

  return (
    <div className="app">
      <header className="app-header">
        <div className="app-header-title">
          <h1>TestOrder</h1>
          <p>Gestão de pedidos</p>
        </div>
        <span className={`status-badge ${systemError ? 'status-badge--warning' : 'status-badge--online'}`}>
          <span className="status-dot" />
          {systemError ? 'Instabilidade detectada' : 'Sistema operacional'}
        </span>
      </header>

      <nav className="tabs" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'orders'}
          className={`tab ${activeTab === 'orders' ? 'tab--active' : ''}`}
          onClick={() => setActiveTab('orders')}
        >
          Pedidos
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'revenue'}
          className={`tab ${activeTab === 'revenue' ? 'tab--active' : ''}`}
          onClick={() => setActiveTab('revenue')}
        >
          Faturamento
        </button>
      </nav>

      {activeTab === 'orders' && <OrdersPage onSystemErrorChange={setSystemError} />}
      {activeTab === 'revenue' && <RevenuePage isActive={activeTab === 'revenue'} />}
    </div>
  );
}
