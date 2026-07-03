const numberFromEnv = (value, fallback) => {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
};

export const config = {
  mysqlHost: process.env.MYSQL_HOST || 'localhost',
  mysqlPort: numberFromEnv(process.env.MYSQL_PORT, 3306),
  mysqlDatabase: process.env.MYSQL_DATABASE || 'testorder',
  mysqlUser: process.env.MYSQL_USER || 'testorder',
  mysqlPassword: process.env.MYSQL_PASSWORD || 'testorder',
  pollIntervalMs: numberFromEnv(process.env.POLL_INTERVAL_MS, 2000),
  batchSize: numberFromEnv(process.env.BATCH_SIZE, 10),
};
