const GENERIC_ERROR_MESSAGE = 'Não foi possível conectar ao servidor. Tente novamente.';

async function readJsonResponse(response) {
  const contentType = response.headers.get('content-type') || '';
  if (!contentType.toLowerCase().includes('application/json')) {
    throw new Error(GENERIC_ERROR_MESSAGE);
  }

  try {
    return await response.json();
  } catch {
    throw new Error(GENERIC_ERROR_MESSAGE);
  }
}

async function parseErrorMessage(response) {
  try {
    const body = await response.json();
    if (body && typeof body.error === 'string' && body.error.trim() !== '') {
      return body.error;
    }
  } catch {
    // resposta sem corpo JSON válido — usa mensagem genérica
  }
  return `Erro inesperado (${response.status}).`;
}

export async function fetchProducts() {
  let response;
  try {
    response = await fetch('/api/products');
  } catch {
    throw new Error(GENERIC_ERROR_MESSAGE);
  }
  if (!response.ok) {
    throw new Error(await parseErrorMessage(response));
  }
  return readJsonResponse(response);
}

export async function fetchOrders(page, pageSize = 20, filters = {}) {
  let response;
  try {
    const params = new URLSearchParams({ page, pageSize });
    const { status, startDate, endDate } = filters;
    if (status) params.set('status', status);
    if (startDate) params.set('startDate', startDate);
    if (endDate) params.set('endDate', endDate);
    response = await fetch(`/api/orders?${params.toString()}`);
  } catch {
    throw new Error(GENERIC_ERROR_MESSAGE);
  }
  if (!response.ok) {
    throw new Error(await parseErrorMessage(response));
  }
  return readJsonResponse(response);
}

export async function createOrder({ customerName, items }) {
  let response;
  try {
    response = await fetch('/api/orders', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ customerName: customerName || null, items }),
    });
  } catch {
    throw new Error(GENERIC_ERROR_MESSAGE);
  }
  if (!response.ok) {
    throw new Error(await parseErrorMessage(response));
  }
  return readJsonResponse(response);
}

export async function fetchDailyRevenue(startDate, endDate) {
  let response;
  try {
    const params = new URLSearchParams();
    if (startDate) params.set('startDate', startDate);
    if (endDate) params.set('endDate', endDate);
    const query = params.toString();
    response = await fetch(`/api/revenue/daily${query ? `?${query}` : ''}`);
  } catch {
    throw new Error(GENERIC_ERROR_MESSAGE);
  }
  if (!response.ok) {
    throw new Error(await parseErrorMessage(response));
  }
  return readJsonResponse(response);
}
