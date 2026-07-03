import { createPool } from 'mysql2/promise';
import { config } from './config.js';
import { processPendingEvents } from './worker.js';

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const pool = createPool({
  host: config.mysqlHost,
  port: config.mysqlPort,
  database: config.mysqlDatabase,
  user: config.mysqlUser,
  password: config.mysqlPassword,
});

let shuttingDown = false;

async function main() {
  console.log(
    JSON.stringify({
      level: 'info',
      action: 'worker-started',
      pollIntervalMs: config.pollIntervalMs,
      batchSize: config.batchSize,
    }),
  );

  while (!shuttingDown) {
    try {
      await processPendingEvents(pool);
    } catch (err) {
      console.error(
        JSON.stringify({
          level: 'error',
          action: 'poll-cycle-failed',
          message: err.message,
        }),
      );
    }

    await sleep(config.pollIntervalMs);
  }

  await pool.end();
  console.log(JSON.stringify({ level: 'info', action: 'worker-stopped' }));
  process.exit(0);
}

process.on('SIGINT', () => {
  // Nao encerra imediatamente: sinaliza o loop para nao iniciar um novo ciclo
  // e deixa o ciclo em andamento concluir seu commit/rollback antes de fechar
  // o pool, evitando conexoes penduradas ou transacoes abortadas no meio.
  console.log(JSON.stringify({ level: 'info', action: 'shutdown-requested' }));
  shuttingDown = true;
});

main();
