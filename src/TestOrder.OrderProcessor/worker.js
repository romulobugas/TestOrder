import { config } from './config.js';

export async function processPendingEvents(pool) {
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();

    // Concorrência segura: FOR UPDATE SKIP LOCKED reserva os eventos pendentes
    // para esta transação sem bloquear outras instâncias do worker, que pulam
    // as linhas já travadas e seguem para as proximas. A marcacao final usa
    // UPDATE ... WHERE status = 'pending', garantindo idempotencia: se outra
    // instancia ja tiver processado a linha (affectedRows === 0), seguimos sem
    // erro em vez de tratar isso como falha.
    const [rows] = await conn.query(
      `SELECT id, order_id, event_type, payload, created_at
       FROM order_processing_events
       WHERE status = 'pending' AND event_type = 'OrderCreated'
       ORDER BY created_at ASC, id ASC
       LIMIT ?
       FOR UPDATE SKIP LOCKED`,
      [config.batchSize],
    );

    for (const row of rows) {
      logEventResult(row);

      const [result] = await conn.query(
        `UPDATE order_processing_events
         SET status = 'processed'
         WHERE id = ? AND status = 'pending'`,
        [row.id],
      );

      if (result.affectedRows === 0) {
        // Outra instancia venceu a corrida entre o SELECT e este UPDATE;
        // nao ha erro a tratar, apenas seguimos para o proximo evento.
        continue;
      }
    }

    await conn.commit();
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

function logEventResult(row) {
  let payload;
  try {
    payload = typeof row.payload === 'string' ? JSON.parse(row.payload) : row.payload;
  } catch (err) {
    console.error(
      JSON.stringify({
        level: 'error',
        action: 'order-created-payload-invalid',
        eventId: row.id,
        orderId: row.order_id,
        reason: err.message,
      }),
    );
    return;
  }

  console.log(
    JSON.stringify({
      level: 'info',
      action: 'order-created-processed',
      eventId: row.id,
      orderId: payload?.orderId ?? row.order_id,
      eventType: row.event_type,
      processedAt: new Date().toISOString(),
    }),
  );
}
