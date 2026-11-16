import { RevitClientConnection } from "./SocketClient.js";
import { findRevitInstances, probeRevit } from "./revitInstances.js";

// Target Revit instance. The start value comes from env (the plugin picks 8080, 8081, ...
// depending on which ports are free); launch_revit and use_revit_instance switch it
// for the rest of the session.
// 127.0.0.1, not localhost: the plugin listens on the IPv4 loopback only.
const target = {
  host: process.env.REVIT_HOST || "127.0.0.1",
  port: Number(process.env.REVIT_PORT) || 8080,
};

export function getRevitTarget(): { host: string; port: number } {
  return { ...target };
}

/** Переключить сессию на другой экземпляр Revit (порт его плагина). */
export function setRevitTarget(port: number, host?: string): void {
  target.port = port;
  if (host) target.host = host;
  // launch_revit и use_revit_instance переключают только на проверенный плагин.
  verifiedPort = port;
}

// Порт, на котором уже подтверждён наш плагин (ответил на mcp_status). Сбрасывается после ошибки.
let verifiedPort: number | null = null;

/**
 * Перед командой: отвечает ли на порту сессии наш плагин. Порт может занять другая программа —
 * например, другая надстройка Revit, пока сервер плагина был выключен кнопкой; такой порт
 * принимает соединение, но не отвечает, и команда висела бы до таймаута. Тогда, если запущенный
 * Revit с плагином ровно один, сессия переключается на него; если их несколько — не угадываем.
 */
async function ensurePluginTarget(): Promise<void> {
  if (verifiedPort === target.port) return;
  if (await probeRevit(target.port, target.host)) {
    verifiedPort = target.port;
    return;
  }
  const instances = await findRevitInstances(target.host);
  if (instances.length === 1) {
    console.error(`На ${target.host}:${target.port} плагин не отвечает; сессия переключена на порт ${instances[0].port}`);
    target.port = instances[0].port;
    verifiedPort = target.port;
  }
}

// Mutex to serialize all Revit connections - prevents race conditions
// when multiple requests are made in parallel
let connectionMutex: Promise<void> = Promise.resolve();

/**
 * Connect to the Revit plugin and run an operation.
 * @param operation Function to run once connected
 * @returns Result of the operation
 */
export async function withRevitConnection<T>(
  operation: (client: RevitClientConnection) => Promise<T>
): Promise<T> {
  // Wait for any pending connection to complete before starting a new one
  const previousMutex = connectionMutex;
  let releaseMutex: () => void;
  connectionMutex = new Promise<void>((resolve) => {
    releaseMutex = resolve;
  });
  await previousMutex;

  try {
    await ensurePluginTarget();
  } catch {
    // Проверка не должна мешать команде: ниже будет обычная ошибка подключения.
  }

  const revitClient = new RevitClientConnection(target.host, target.port);
  const where = `${target.host}:${target.port}`;
  const hint =
    "Запустите Revit с сервером (launch_revit) или выберите работающий экземпляр (list_revit_instances, use_revit_instance).";

  try {
    // Connect to the Revit plugin
    if (!revitClient.isConnected) {
      await new Promise<void>((resolve, reject) => {
        let timer: NodeJS.Timeout | undefined;
        const cleanup = () => {
          if (timer) clearTimeout(timer);
          revitClient.socket.removeListener("connect", onConnect);
          revitClient.socket.removeListener("error", onError);
        };

        const onConnect = () => {
          cleanup();
          resolve();
        };

        const onError = (error: any) => {
          cleanup();
          reject(new Error(`Revit не отвечает на ${where}: сервер плагина не запущен. ${hint}`));
        };

        revitClient.socket.on("connect", onConnect);
        revitClient.socket.on("error", onError);

        revitClient.connect();

        timer = setTimeout(() => {
          cleanup();
          reject(new Error(`Revit не ответил на ${where} за 5 с. ${hint}`));
        }, 5000);
      });
    }

    // Run the operation
    return await operation(revitClient);
  } catch (error) {
    // После ошибки порт проверяется заново: Revit могли перезапустить или сервер плагина переехать.
    verifiedPort = null;
    throw error;
  } finally {
    // Disconnect
    revitClient.disconnect();
    // Release the mutex so the next request can proceed
    releaseMutex!();
  }
}
