import AsyncStorage from '@react-native-async-storage/async-storage';
import {
  ConnectionStatus,
  DEFAULT_REMOTE_STATE,
  RemoteCommand,
  RemoteState,
  SearchTarget,
} from '../types/remote';

const STORAGE_LAST_HOST = '@zenbox_remote_last_host';
const STORAGE_RECENT_HOSTS = '@zenbox_remote_recent_hosts';
const DEFAULT_PORT = 8989;

type StatusListener = (status: ConnectionStatus, host: string) => void;
type StateListener = (state: RemoteState) => void;

class ZenBoxSocketService {
  private ws: WebSocket | null = null;
  private host: string = '';
  private port: number = DEFAULT_PORT;
  private status: ConnectionStatus = 'disconnected';
  private state: RemoteState = { ...DEFAULT_REMOTE_STATE };
  private shouldReconnect: boolean = false;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;

  private statusListeners = new Set<StatusListener>();
  private stateListeners = new Set<StateListener>();

  public async initialize(): Promise<string | null> {
    try {
      const savedHost = await AsyncStorage.getItem(STORAGE_LAST_HOST);
      if (savedHost && savedHost.trim().length > 0) {
        this.connect(savedHost.trim());
        return savedHost.trim();
      }
    } catch {
      // Ignore storage read failure
    }
    return null;
  }

  public async getRecentHosts(): Promise<string[]> {
    try {
      const raw = await AsyncStorage.getItem(STORAGE_RECENT_HOSTS);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed)) {
          return parsed.filter((x): x is string => typeof x === 'string');
        }
      }
    } catch {
      // Ignore
    }
    return [];
  }

  private async saveHostToHistory(cleanHost: string): Promise<void> {
    try {
      await AsyncStorage.setItem(STORAGE_LAST_HOST, cleanHost);
      const existing = await this.getRecentHosts();
      const updated = [cleanHost, ...existing.filter((h) => h !== cleanHost)].slice(0, 5);
      await AsyncStorage.setItem(STORAGE_RECENT_HOSTS, JSON.stringify(updated));
    } catch {
      // Ignore
    }
  }

  public getStatus(): ConnectionStatus {
    return this.status;
  }

  public getHost(): string {
    return this.host;
  }

  public getState(): RemoteState {
    return this.state;
  }

  public subscribeStatus(listener: StatusListener): () => void {
    this.statusListeners.add(listener);
    listener(this.status, this.host);
    return () => {
      this.statusListeners.delete(listener);
    };
  }

  public subscribeState(listener: StateListener): () => void {
    this.stateListeners.add(listener);
    listener(this.state);
    return () => {
      this.stateListeners.delete(listener);
    };
  }

  private notifyStatus(next: ConnectionStatus): void {
    this.status = next;
    this.statusListeners.forEach((cb) => cb(this.status, this.host));
  }

  private notifyState(next: RemoteState): void {
    this.state = next;
    this.stateListeners.forEach((cb) => cb(this.state));
  }

  public connect(rawHostInput: string): void {
    const cleaned = rawHostInput
      .trim()
      .replace(/^ws:\/\//i, '')
      .replace(/^http:\/\//i, '')
      .replace(/\/.*$/, '');

    if (!cleaned) {
      return;
    }

    let targetIp = cleaned;
    let targetPort = DEFAULT_PORT;
    if (cleaned.includes(':')) {
      const [ipPart, portPart] = cleaned.split(':');
      targetIp = ipPart.trim();
      const parsedPort = parseInt(portPart, 10);
      if (!Number.isNaN(parsedPort) && parsedPort > 0 && parsedPort <= 65535) {
        targetPort = parsedPort;
      }
    }

    this.clearReconnectTimer();
    this.closeActiveSocketOnly();

    this.host = targetPort === DEFAULT_PORT ? targetIp : `${targetIp}:${targetPort}`;
    this.port = targetPort;
    this.shouldReconnect = true;
    this.openWebSocket(targetIp, targetPort);
  }

  private openWebSocket(ip: string, port: number): void {
    this.notifyStatus('connecting');
    const url = `ws://${ip}:${port}`;

    try {
      const socket = new WebSocket(url);
      this.ws = socket;

      socket.onopen = () => {
        if (this.ws !== socket) return;
        this.notifyStatus('connected');
        void this.saveHostToHistory(this.host);
        this.sendCommand({ action: 'GET_STATE' });
      };

      socket.onmessage = (event) => {
        if (this.ws !== socket) return;
        try {
          const parsed = JSON.parse(String(event.data));
          if (parsed && parsed.type === 'STATE') {
            this.notifyState({
              ...DEFAULT_REMOTE_STATE,
              ...parsed,
            });
          }
        } catch {
          // Ignore malformed message
        }
      };

      socket.onerror = () => {
        // Handled in onclose
      };

      socket.onclose = () => {
        if (this.ws !== socket) return;
        this.ws = null;
        this.notifyStatus('disconnected');
        if (this.shouldReconnect && ip) {
          this.clearReconnectTimer();
          this.reconnectTimer = setTimeout(() => {
            if (this.shouldReconnect) {
              this.openWebSocket(ip, port);
            }
          }, 2600);
        }
      };
    } catch {
      this.notifyStatus('disconnected');
    }
  }

  public disconnect(): void {
    this.shouldReconnect = false;
    this.clearReconnectTimer();
    this.closeActiveSocketOnly();
    this.notifyStatus('disconnected');
  }

  private closeActiveSocketOnly(): void {
    if (this.ws) {
      const old = this.ws;
      this.ws = null;
      try {
        old.onopen = null;
        old.onmessage = null;
        old.onerror = null;
        old.onclose = null;
        old.close();
      } catch {
        // Ignore
      }
    }
  }

  private clearReconnectTimer(): void {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }

  public sendCommand(command: RemoteCommand): boolean {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
      return false;
    }
    try {
      this.ws.send(JSON.stringify(command));
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Gửi từng nhịp gõ phím thời gian thực lên TV với độ trễ cực thấp (70ms)
   * để thanh tìm kiếm YouTube / Spotify / Netflix / Kho Phim cập nhật tức thì khi đang gõ!
   */
  public sendRealtimeSearch(text: string, target: SearchTarget): void {
    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
    }

    this.searchDebounceTimer = setTimeout(() => {
      this.sendCommand({
        action: 'SEARCH_REALTIME',
        target,
        text,
      });
    }, 70);
  }

  public submitSearch(text: string, target: SearchTarget): void {
    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
      this.searchDebounceTimer = null;
    }

    this.sendCommand({
      action: 'SEARCH_SUBMIT',
      target,
      text,
    });
  }

  /**
   * Quét nhanh mạng Wi-Fi nội bộ qua cổng 8989 để tự động phát hiện máy tính chạy ZenBox.
   */
  public async scanLocalNetwork(
    subnetHint?: string,
    onProgress?: (percent: number) => void
  ): Promise<string[]> {
    const prefixes = new Set<string>();

    const extractPrefix = (ip: string | undefined) => {
      if (!ip) return;
      const clean = ip.trim().split(':')[0];
      const parts = clean.split('.');
      if (parts.length >= 3 && parts.slice(0, 3).every((p) => /^\d+$/.test(p))) {
        prefixes.add(`${parts[0]}.${parts[1]}.${parts[2]}`);
      }
    };

    extractPrefix(subnetHint);
    extractPrefix(this.host);
    prefixes.add('192.168.1');
    prefixes.add('192.168.0');
    prefixes.add('192.168.100');

    const candidates: string[] = [];
    Array.from(prefixes)
      .slice(0, 3)
      .forEach((prefix) => {
        for (let i = 2; i <= 254; i++) {
          candidates.push(`${prefix}.${i}`);
        }
      });

    const found: string[] = [];
    const batchSize = 36;

    for (let idx = 0; idx < candidates.length; idx += batchSize) {
      const batch = candidates.slice(idx, idx + batchSize);
      const results = await Promise.all(batch.map((ip) => this.probeSingleHost(ip, DEFAULT_PORT, 420)));
      results.forEach((matchedIp) => {
        if (matchedIp && !found.includes(matchedIp)) {
          found.push(matchedIp);
        }
      });

      if (onProgress) {
        const pct = Math.min(100, Math.round(((idx + batch.length) / candidates.length) * 100));
        onProgress(pct);
      }

      // Dừng sớm ngay khi đã tìm thấy máy chủ ZenBox trong dải mạng hiện tại
      if (found.length > 0 && idx >= 250) {
        onProgress?.(100);
        break;
      }
    }

    return found;
  }

  private probeSingleHost(ip: string, port: number, timeoutMs: number): Promise<string | null> {
    return new Promise((resolve) => {
      let settled = false;
      let probeWs: WebSocket | null = null;

      const finish = (result: string | null) => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        if (probeWs) {
          try {
            probeWs.onopen = null;
            probeWs.onmessage = null;
            probeWs.onerror = null;
            probeWs.onclose = null;
            probeWs.close();
          } catch {
            // Ignore
          }
        }
        resolve(result);
      };

      const timer = setTimeout(() => finish(null), timeoutMs);

      try {
        probeWs = new WebSocket(`ws://${ip}:${port}`);
        probeWs.onopen = () => {
          try {
            probeWs?.send(JSON.stringify({ action: 'PING' }));
          } catch {
            finish(ip);
          }
        };
        probeWs.onmessage = (ev) => {
          const data = String(ev.data || '');
          if (data.includes('"STATE"') || data.includes('activeSection')) {
            finish(ip);
          } else {
            finish(ip);
          }
        };
        probeWs.onerror = () => finish(null);
        probeWs.onclose = () => finish(null);
      } catch {
        finish(null);
      }
    });
  }
}

export const zenSocket = new ZenBoxSocketService();
