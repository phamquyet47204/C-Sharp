import * as signalR from '@microsoft/signalr';

let connection;

const resolveApiBase = () => {
  const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim();
  if (configuredBaseUrl) {
    const url = new URL(configuredBaseUrl, window.location.origin);
    return `${url.origin}${url.pathname}`.replace(/\/$/, '');
  }
  return `${window.location.origin}/api`;
};

const getHubUrl = () => {
  const apiBase = resolveApiBase();
  const normalized = apiBase.replace(/\/api\/?$/i, '');
  return `${normalized}/hubs/analytics`;
};

const getAccessToken = () => localStorage.getItem('token') || '';

export const startRealtimeAnalytics = async (onMessage) => {
  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
    return connection;
  }

  connection = new signalR.HubConnectionBuilder()
    .withUrl(getHubUrl(), {
      accessTokenFactory: getAccessToken,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .build();

  connection.on('analytics:realtime', (payload) => {
    if (typeof onMessage === 'function') {
      onMessage(payload);
    }
  });

  await connection.start();
  return connection;
};

export const stopRealtimeAnalytics = async () => {
  if (!connection) return;
  await connection.stop();
  connection = undefined;
};
