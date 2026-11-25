import { Platform } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Constants from 'expo-constants';
import * as Network from 'expo-network';
import {
  ConnectionStatus,
  DEFAULT_REMOTE_STATE,
  DiscoveredZenBoxDevice,
  RemoteCommand,
  RemoteState,
  SearchTarget,
} from '../types/remote';

const STORAGE_LAST_HOST = '@zenbox_remote_last_host_v2';
const STORAGE_RECENT_HOSTS = '@zenbox_remote_recent_hosts_v2';
const STORAGE_DEVICE_ID = '@zenbox_remote_device_id';
const DEFAULT_PORT = 8989;

type StatusListener = (status: ConnectionStatus, host: string) => void;
type StateListener = (state: RemoteState) => void;
type UnconnectedListener = () => void;

class ZenBoxSocketService {
  private ws: WebSocket | null = null;
  private targetIp: string = '';
  private targetPort: number = DEFAULT_PORT;
  private hostDisplay: string = '';
  private status: ConnectionStatus = 'disconnected';
  private state: RemoteState = { ...DEFAULT_REMOTE_STATE };
  private deviceId: string = '';
  private deviceName: string = Platform.OS === 'ios' ? 'iPhone' : 'Điện thoại Android';

  private shouldReconnect: boolean = false;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;

  private statusListeners = new Set<StatusListener>();
  private stateListeners = new Set<StateListener>();
  private unconnectedListeners = new Set<UnconnectedListener>();

  public async initialize(): Promise<{ connected: boolean; host: string | null }> {
    await this.ensureDeviceId();

    // 1. Thử kết nối lại vào địa chỉ IP đã lưu thành công gần nhất (nếu máy chủ đang bật)
    try {
      const savedHost = await AsyncStorage.getItem(STORAGE_LAST_HOST);
      if (savedHost && savedHost.trim().length > 0 && !savedHost.trim().startsWith('26.')) {
        const ok = await this.connectAndVerify(savedHost.trim());
        if (ok) {
          return { connected: true, host: this.hostDisplay };
        }
      }
    } catch {
      // Ignore
    }

    // 2. Nếu chạy qua Expo Go, lấy ngay IP máy tính từ Metro hostUri (kết nối tự động < 100ms!)
    const expoHostIp = this.getExpoHostIp();
    if (expoHostIp && !expoHostIp.startsWith('26.')) {
      const ok = await this.connectAndVerify(expoHostIp);
      if (ok) {
        return { connected: true, host: this.hostDisplay };
      }
    }

    return { connected: false, host: null };
  }

  private async ensureDeviceId(): Promise<void> {
    if (this.deviceId) return;
    try {
      const saved = await AsyncStorage.getItem(STORAGE_DEVICE_ID);
      if (saved) {
        this.deviceId = saved;
        return;
      }
      const generated = `phone_${Math.random().toString(36).slice(2, 10)}`;
      this.deviceId = generated;
      await AsyncStorage.setItem(STORAGE_DEVICE_ID, generated);
    } catch {
      this.deviceId = `phone_${Date.now()}`;
    }
  }

  public getExpoHostIp(): string | null {
    try {
      const rawUri =
        Constants.expoConfig?.hostUri ||
        (Constants as unknown as { manifest2?: { extra?: { expoGo?: { debuggerHost?: string } } } })
          ?.manifest2?.extra?.expoGo?.debuggerHost ||
        '';
      if (!rawUri) return null;
      const ipPart = rawUri.split(':')[0]?.trim();
      if (ipPart && /^\d+\.\d+\.\d+\.\d+$/.test(ipPart)) {
        return ipPart;
      }
    } catch {
      // Ignore
    }
    return null;
  }

  public async getPhoneWifiIp(): Promise<string | null> {
    try {
      const ip = await Network.getIpAddressAsync();
      if (ip && /^\d+\.\d+\.\d+\.\d+$/.test(ip) && ip !== '0.0.0.0' && ip !== '127.0.0.1') {
        return ip;
      }
    } catch {
      // Ignore
    }
    return this.getExpoHostIp();
  }

  public async getRecentHosts(): Promise<string[]> {
    try {
      const raw = await AsyncStorage.getItem(STORAGE_RECENT_HOSTS);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed)) {
          return parsed.filter(
            (x): x is string => typeof x === 'string' && !x.startsWith('26.')
          );
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

  public subscribeStatus(listener: StatusListener): () => void {
    this.statusListeners.add(listener);
    listener(this.status, this.hostDisplay);
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

  public subscribeUnconnectedAttempt(listener: UnconnectedListener): () => void {
    this.unconnectedListeners.add(listener);
    return () => {
      this.unconnectedListeners.delete(listener);
    };
  }

  private notifyStatus(next: ConnectionStatus): void {
    this.status = next;
    this.statusListeners.forEach((cb) => cb(this.status, this.hostDisplay));
  }

  private notifyState(next: RemoteState): void {
    this.state = next;
    this.stateListeners.forEach((cb) => cb(this.state));
  }

  private parseHostAndPort(rawInput: string): { ip: string; port: number } | null {
    const cleaned = rawInput
      .trim()
      .replace(/^ws:\/\//i, '')
      .replace(/^http:\/\//i, '')
      .replace(/\/.*$/, '');

    if (!cleaned) return null;

    let ip = cleaned;
    let port = DEFAULT_PORT;
    if (cleaned.includes(':')) {
      const [ipPart, portPart] = cleaned.split(':');
      ip = ipPart.trim();
      const parsedPort = parseInt(portPart, 10);
      if (!Number.isNaN(parsedPort) && parsedPort > 0 && parsedPort <= 65535) {
        port = parsedPort;
      }
    }
    if (!ip) return null;
    return { ip, port };
  }

  /**
   * Kiểm tra nhanh qua HTTP GET /zenbox-info và mở kết nối WebSocket đa thiết bị.
   * Trả về true ngay khi kết nối thành công để thông báo chính xác cho người dùng.
   */
  public async connectAndVerify(rawHostInput: string): Promise<boolean> {
    const parsed = this.parseHostAndPort(rawHostInput);
    if (!parsed) {
      return false;
    }

    const { ip, port } = parsed;
    this.clearReconnectTimer();
    this.closeActiveSocketOnly();

    this.targetIp = ip;
    this.targetPort = port;
    this.hostDisplay = port === DEFAULT_PORT ? ip : `${ip}:${port}`;
    this.shouldReconnect = true;
    this.notifyStatus('connecting');

    // Bước 1: Kiểm tra qua HTTP /zenbox-info (cực nhạy và lấy luôn trạng thái ban đầu)
    const info = await this.probeHttpInfo(ip, port, 1800);
    if (info) {
      if (info.state) {
        this.notifyState({ ...DEFAULT_REMOTE_STATE, ...info.state });
      }
      this.notifyStatus('connected');
      void this.saveHostToHistory(this.hostDisplay);
      this.openWebSocket(ip, port);
      return true;
    }

    // Bước 2: Thử mở trực tiếp WebSocket với thời gian chờ 2.2s
    const wsConnected = await this.openWebSocketWithPromise(ip, port, 2200);
    if (wsConnected) {
      void this.saveHostToHistory(this.hostDisplay);
      return true;
    }

    this.shouldReconnect = false;
    this.notifyStatus('disconnected');
    return false;
  }

  private openWebSocketWithPromise(
    ip: string,
    port: number,
    timeoutMs: number
  ): Promise<boolean> {
    return new Promise((resolve) => {
      let settled = false;
      const timer = setTimeout(() => {
        if (!settled) {
          settled = true;
          resolve(false);
        }
      }, timeoutMs);

      this.openWebSocket(
        ip,
        port,
        () => {
          if (!settled) {
            settled = true;
            clearTimeout(timer);
            resolve(true);
          }
        },
        () => {
          if (!settled) {
            settled = true;
            clearTimeout(timer);
            resolve(false);
          }
        }
      );
    });
  }

  private openWebSocket(
    ip: string,
    port: number,
    onFirstOpen?: () => void,
    onFirstFail?: () => void
  ): void {
    try {
      const socket = new WebSocket(`ws://${ip}:${port}`);
      this.ws = socket;

      socket.onopen = () => {
        if (this.ws !== socket) return;
        this.notifyStatus('connected');
        onFirstOpen?.();
        try {
          socket.send(
            JSON.stringify({
              action: 'GET_STATE',
              deviceId: this.deviceId,
              deviceName: this.deviceName,
            })
          );
        } catch {
          // Ignore
        }
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
          // Ignore
        }
      };

      socket.onerror = () => {
        onFirstFail?.();
      };

      socket.onclose = () => {
        if (this.ws !== socket) return;
        this.ws = null;
        onFirstFail?.();

        if (this.shouldReconnect && ip) {
          this.clearReconnectTimer();
          this.reconnectTimer = setTimeout(() => {
            if (this.shouldReconnect) {
              this.openWebSocket(ip, port);
            }
          }, 2000);
        } else {
          this.notifyStatus('disconnected');
        }
      };
    } catch {
      onFirstFail?.();
      this.notifyStatus('disconnected');
    }
  }

  public disconnect(): void {
    this.shouldReconnect = false;
    this.targetIp = '';
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

  /**
   * Gửi lệnh ưu tiên qua WebSocket tốc độ cao; nếu WebSocket đang tự nối lại ngầm,
   * tự động chuyển sang HTTP POST /zenbox-cmd để mọi nút bấm luôn ăn ngay 100%!
   */
  public sendCommand(command: RemoteCommand): boolean {
    if (!this.targetIp) {
      this.unconnectedListeners.forEach((cb) => cb());
      return false;
    }

    const enriched: RemoteCommand = {
      ...command,
      deviceId: this.deviceId,
      deviceName: this.deviceName,
    };

    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      try {
        this.ws.send(JSON.stringify(enriched));
        return true;
      } catch {
        // Fall through to HTTP fallback
      }
    }

    // Dự phòng HTTP POST tức thì lên cùng cổng 8989
    void this.sendHttpFallback(enriched);
    if (!this.ws && this.shouldReconnect) {
      this.openWebSocket(this.targetIp, this.targetPort);
    }
    return true;
  }

  private async sendHttpFallback(command: RemoteCommand): Promise<void> {
    if (!this.targetIp) return;
    try {
      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), 1500);
      const resp = await fetch(`http://${this.targetIp}:${this.targetPort}/zenbox-cmd`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(command),
        signal: controller.signal,
      });
      clearTimeout(timer);
      if (resp.ok) {
        const nextState = await resp.json();
        if (nextState && nextState.type === 'STATE') {
          this.notifyState({ ...DEFAULT_REMOTE_STATE, ...nextState });
          if (this.status !== 'connected') {
            this.notifyStatus('connected');
          }
        }
      }
    } catch {
      // Ignore network drop
    }
  }

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
    }, 65);
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
   * Tự động phát hiện đúng dải mạng Wi-Fi thật của điện thoại (VD: 192.168.2.x, 192.168.1.x, 10.0.x.x...)
   * và quét qua HTTP GET /zenbox-info siêu tốc.
   */
  public async scanLocalNetwork(
    subnetHint?: string,
    onProgress?: (percent: number) => void
  ): Promise<DiscoveredZenBoxDevice[]> {
    const found: DiscoveredZenBoxDevice[] = [];

    // 1. Thử ngay IP của Metro Expo Host trước tiên (chỉ mất ~30ms nếu trúng!)
    const expoIp = this.getExpoHostIp();
    if (expoIp && !expoIp.startsWith('26.')) {
      const instant = await this.probeHttpInfo(expoIp, DEFAULT_PORT, 600);
      if (instant) {
        found.push({
          ip: expoIp,
          port: DEFAULT_PORT,
          serverName: instant.serverName || 'ZenBox PC',
          connectedClients: instant.connectedClients ?? 0,
        });
        onProgress?.(100);
        return found;
      }
    }

    // 2. Xác định dải mạng Wi-Fi hiện tại của chính chiếc điện thoại này
    const prefixes: string[] = [];
    const addPrefixFromIp = (ipStr: string | null | undefined) => {
      if (!ipStr) return;
      const clean = ipStr.trim().split(':')[0];
      const parts = clean.split('.');
      if (
        parts.length >= 3 &&
        parts.slice(0, 3).every((p) => /^\d+$/.test(p)) &&
        parts[0] !== '26' &&
        parts[0] !== '127'
      ) {
        const pfx = `${parts[0]}.${parts[1]}.${parts[2]}`;
        if (!prefixes.includes(pfx)) {
          prefixes.push(pfx);
        }
      }
    };

    const phoneWifiIp = await this.getPhoneWifiIp();
    addPrefixFromIp(phoneWifiIp);
    addPrefixFromIp(subnetHint);
    addPrefixFromIp(this.targetIp);
    addPrefixFromIp('192.168.2.1');
    addPrefixFromIp('192.168.1.1');
    addPrefixFromIp('192.168.0.1');

    // Quét tối đa 2 dải mạng ưu tiên nhất (dải Wi-Fi thực của điện thoại luôn đứng số 1!)
    const selectedPrefixes = prefixes.slice(0, 2);
    const candidates: string[] = [];
    selectedPrefixes.forEach((prefix) => {
      for (let hostId = 1; hostId <= 254; hostId++) {
        const candidateIp = `${prefix}.${hostId}`;
        if (candidateIp !== phoneWifiIp) {
          candidates.push(candidateIp);
        }
      }
    });

    const batchSize = 26;
    for (let idx = 0; idx < candidates.length; idx += batchSize) {
      const batch = candidates.slice(idx, idx + batchSize);
      const batchResults = await Promise.all(
        batch.map(async (ip) => {
          const info = await this.probeHttpInfo(ip, DEFAULT_PORT, 650);
          if (!info) return null;
          return {
            ip,
            port: info.port || DEFAULT_PORT,
            serverName: info.serverName || 'ZenBox PC',
            connectedClients: info.connectedClients ?? 0,
          } as DiscoveredZenBoxDevice;
        })
      );

      batchResults.forEach((dev) => {
        if (dev && !found.some((f) => f.ip === dev.ip)) {
          found.push(dev);
        }
      });

      if (onProgress) {
        const pct = Math.min(100, Math.round(((idx + batch.length) / candidates.length) * 100));
        onProgress(pct);
      }

      // Nếu đã quét hết dải mạng đầu tiên (dải của điện thoại) và tìm thấy TV thì trả về ngay!
      if (found.length > 0 && idx + batchSize >= 253) {
        onProgress?.(100);
        break;
      }
    }

    return found;
  }

  private async probeHttpInfo(
    ip: string,
    port: number,
    timeoutMs: number
  ): Promise<{
    app?: string;
    serverName?: string;
    port?: number;
    connectedClients?: number;
    state?: RemoteState;
  } | null> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    try {
      const resp = await fetch(`http://${ip}:${port}/zenbox-info`, {
        method: 'GET',
        signal: controller.signal,
      });
      clearTimeout(timer);
      if (!resp.ok) return null;
      const data = await resp.json();
      if (data && data.app === 'ZenBox') {
        return data;
      }
      return null;
    } catch {
      clearTimeout(timer);
      return null;
    }
  }
}

export const zenSocket = new ZenBoxSocketService();
