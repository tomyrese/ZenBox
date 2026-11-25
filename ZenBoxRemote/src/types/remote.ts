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
}

export interface RemoteState {
  type: string;
  activeSection: string;
  activeTitle: string;
  volume: number;
  isMuted: boolean;
  audioPreset: string;
  audioPresetLabel: string;
  eyeCare: string;
  sleepTimer: string;
}

export const DEFAULT_REMOTE_STATE: RemoteState = {
  type: 'STATE',
  activeSection: 'Home',
  activeTitle: 'Trang Chủ ZenBox',
  volume: 60,
  isMuted: false,
  audioPreset: 'Balanced',
  audioPresetLabel: 'Chuẩn cân bằng',
  eyeCare: 'Off',
  sleepTimer: 'Hẹn giờ: Tắt',
};
