import React, { useEffect, useRef, useState } from 'react';
import { fetchDailyRevenue } from '../../api/api.js';
import PageNav from '../../components/PageNav.jsx';
import { DATE_PRESETS, getDefaultRevenueRange, getRecentRange } from '../../shared/dateRanges.js';
import { formatCalendarDate, formatCurrency } from '../../shared/formatters.js';

const DAYS_PAGE_SIZE = 10;

export default function RevenuePage({ isActive }) {
  const [{ startDate, endDate }, setRevenueRange] = useState(getDefaultRevenueRange);
  const [revenue, setRevenue] = useState(null);
  const [loadingRevenue, setLoadingRevenue] = useState(false);
  const [revenueError, setRevenueError] = useState(null);
  const [daysPage, setDaysPage] = useState(1);

  const revenueRequestIdRef = useRef(0);

  useEffect(() => {
    setDaysPage(1);
  }, [revenue]);

  function handlePreset(dayCount) {
    setRevenueRange(getRecentRange(dayCount));
    setRevenueError(null);
  }

  function handleClearRange() {
    setRevenueRange({ startDate: '', endDate: '' });
    setRevenueError(null);
  }

  async function handleConsult(event) {
    event.preventDefault();
    setRevenueError(null);

    if (startDate && endDate && startDate > endDate) {
      setRevenueError('A data inicial não pode ser posterior à data final.');
      return;
    }

    const requestId = revenueRequestIdRef.current + 1;
    revenueRequestIdRef.current = requestId;
    setLoadingRevenue(true);

    const isStale = () => revenueRequestIdRef.current !== requestId || !isActive;

    try {
      const data = await fetchDailyRevenue(startDate, endDate);
      if (isStale()) return;
      setRevenue(data);
    } catch (err) {
      if (isStale()) return;
      setRevenueError(err.message);
    } finally {
      if (revenueRequestIdRef.current === requestId) {
        setLoadingRevenue(false);
      }
    }
  }

  const days = revenue?.days ?? [];
  const totalDaysPages = Math.max(1, Math.ceil(days.length / DAYS_PAGE_SIZE));
  const safeDaysPage = Math.min(daysPage, totalDaysPages);
  const visibleDays = days.slice((safeDaysPage - 1) * DAYS_PAGE_SIZE, safeDaysPage * DAYS_PAGE_SIZE);

  return (
    <main className="app-main app-main--single">
      <section className="panel revenue-panel">
        <h2>Faturamento por período</h2>

        <form className="revenue-form" onSubmit={handleConsult}>
          <div className="form-row">
            <label htmlFor="revenueStartDate">Data inicial</label>
            <input
              id="revenueStartDate"
              type="date"
              value={startDate}
              onChange={(e) => setRevenueRange((prev) => ({ ...prev, startDate: e.target.value }))}
              onDoubleClick={() => setRevenueRange((prev) => ({ ...prev, startDate: '' }))}
            />
          </div>
          <div className="form-row">
            <label htmlFor="revenueEndDate">Data final</label>
            <input
              id="revenueEndDate"
              type="date"
              value={endDate}
              onChange={(e) => setRevenueRange((prev) => ({ ...prev, endDate: e.target.value }))}
              onDoubleClick={() => setRevenueRange((prev) => ({ ...prev, endDate: '' }))}
            />
          </div>
          <button type="submit" className="btn-primary" disabled={loadingRevenue}>
            {loadingRevenue ? 'Consultando...' : 'Consultar'}
          </button>
        </form>

        <div className="date-presets" aria-label="Atalhos de período">
          {DATE_PRESETS.map((preset) => (
            <button
              key={preset.label}
              type="button"
              className="btn-secondary date-preset-button"
              onClick={() => handlePreset(preset.dayCount)}
              disabled={loadingRevenue}
            >
              {preset.label}
            </button>
          ))}
          <button
            type="button"
            className="btn-link date-clear-button"
            onClick={handleClearRange}
            disabled={loadingRevenue}
          >
            Limpar datas
          </button>
        </div>

        {revenueError && <p className="error-message">{revenueError}</p>}
        {loadingRevenue && <p className="hint">Consultando faturamento...</p>}

        {!loadingRevenue && revenue && (
          <>
            <div className="revenue-summary">
              <span className="stat-chip">Total: {formatCurrency(revenue.totalRevenue)}</span>
              <span className="stat-chip">{revenue.totalOrders} pedidos</span>
            </div>

            {days.length === 0 ? (
              <p className="hint">Nenhum dia com faturamento no período informado.</p>
            ) : (
              <>
                <div className="revenue-table-wrapper">
                  <table className="revenue-table">
                    <thead>
                      <tr>
                        <th>Data</th>
                        <th>Pedidos</th>
                        <th>Faturamento</th>
                      </tr>
                    </thead>
                    <tbody>
                      {visibleDays.map((day) => (
                        <tr key={day.date}>
                          <td>{formatCalendarDate(day.date)}</td>
                          <td>{day.orderCount}</td>
                          <td>{formatCurrency(day.revenue)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <PageNav page={safeDaysPage} totalPages={totalDaysPages} onGoToPage={setDaysPage} />
              </>
            )}
          </>
        )}
      </section>
    </main>
  );
}
