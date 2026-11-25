export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected';

export type RemoteTab = 'dpad' | 'touchpad' | 'apps' | 'settings';

export type SearchTarget =
  | 'CURRENT'
  | 'YOUTUBE'
  | 'SPOTIFY'
  | 'NETFLIX'
  | 'TIKTOK'
  | 'GOOGLE'
  | 'OFFLINE';

export interface RemoteCommand {
  action: string;
  target?: string;
  text?: string;
  value?: number;
  dx?: number;
  dy?: number;
  deviceId?: string;
  deviceName?: string;
}

export interface RemoteState {
  type: string;
  serverName?: string;
  activeSection: string;
  activeTitle: string;
  volume: number;
  isMuted: boolean;
  audioPreset: string;
  audioPresetLabel: string;
  eyeCare: string;
  sleepTimer: string;
  connectedClients?: number;
}

export interface DiscoveredZenBoxDevice {
  ip: string;
  port: number;
  serverName: string;
  connectedClients: number;
}

export const DEFAULT_REMOTE_STATE: RemoteState = {
  type: 'STATE',
  serverName: 'ZenBox TV',
  activeSection: 'Home',
  activeTitle: 'Trang Chủ ZenBox',
  volume: 60,
  isMuted: false,
  audioPreset: 'Balanced',
  audioPresetLabel: 'Chuẩn cân bằng',
  eyeCare: 'Off',
  sleepTimer: 'Hẹn giờ: Tắt',
  connectedClients: 0,
};
