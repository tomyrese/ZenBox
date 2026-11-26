import React, { useEffect, useState } from 'react';
import {
  Platform,
  SafeAreaView,
  ScrollView,
  StatusBar as RNStatusBar,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { StatusBar } from 'expo-status-bar';
import { LinearGradient } from 'expo-linear-gradient';
import * as Haptics from 'expo-haptics';

import { Colors } from './src/theme/colors';
import {
  ConnectionStatus,
  DEFAULT_REMOTE_STATE,
  RemoteState,
} from './src/types/remote';
import { zenSocket } from './src/services/ZenBoxSocket';
import { RealtimeSearchBar } from './src/components/RealtimeSearchBar';
import { TouchpadSurface } from './src/components/TouchpadTab';
import { AppBrandKey, BrandLogo } from './src/components/BrandIcons';
import { ConnectionModal } from './src/components/ConnectionModal';

const QUICK_CHANNELS: { target: string; brand: AppBrandKey }[] = [
  { target: 'YOUTUBE', brand: 'YOUTUBE' },
  { target: 'NETFLIX', brand: 'NETFLIX' },
  { target: 'SPOTIFY', brand: 'SPOTIFY' },
  { target: 'TIKTOK', brand: 'TIKTOK' },
  { target: 'VTVGO', brand: 'VTVGO' },
  { target: 'FPTPLAY', brand: 'FPTPLAY' },
  { target: 'FACEBOOK', brand: 'FACEBOOK' },
  { target: 'WEB', brand: 'WEB' },
  { target: 'MOVIES', brand: 'MOVIES' },
  { target: 'MUSIC', brand: 'MUSIC' },
];

export default function App() {
  const [status, setStatus] = useState<ConnectionStatus>('disconnected');
  const [host, setHost] = useState<string>('');
  const [remoteState, setRemoteState] = useState<RemoteState>(DEFAULT_REMOTE_STATE);
  const [padMode, setPadMode] = useState<'dpad' | 'touchpad'>('dpad');
  const [connectModalVisible, setConnectModalVisible] = useState<boolean>(false);

  useEffect(() => {
    const unsubStatus = zenSocket.subscribeStatus((nextStatus, nextHost) => {
      setStatus(nextStatus);
      setHost(nextHost);
    });
    const unsubState = zenSocket.subscribeState((nextState) => {
      setRemoteState(nextState);
    });
    const unsubUnconnected = zenSocket.subscribeUnconnectedAttempt(() => {
      void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Warning);
      setConnectModalVisible(true);
    });

    void zenSocket.initialize().then(({ connected }) => {
      if (!connected) {
        setConnectModalVisible(true);
      }
    });

    return () => {
      unsubStatus();
      unsubState();
      unsubUnconnected();
    };
  }, []);

  const pressRemote = (action: string, strong = false, target?: string) => {
    void Haptics.impactAsync(
      strong ? Haptics.ImpactFeedbackStyle.Medium : Haptics.ImpactFeedbackStyle.Light
    );
    zenSocket.sendCommand({ action, target });
  };

  const isConnected = status === 'connected';
  const statusText =
    status === 'connected'
      ? 'Đã kết nối TV'
      : status === 'connecting'
      ? 'Đang tìm TV...'
      : 'Kết nối TV';

  const dotColor =
    status === 'connected'
      ? Colors.success
      : status === 'connecting'
      ? Colors.warning
      : Colors.danger;

  return (
    <LinearGradient colors={['#060A14', '#0B1326']} style={styles.root}>
      <StatusBar style="light" />
      <SafeAreaView style={styles.safeArea}>
        <View style={styles.screen}>
          {/* 1. Thanh trạng thái tối giản */}
          <View style={styles.topBar}>
            <Text style={styles.appTitle}>ZenBox</Text>

            <TouchableOpacity
              style={[styles.statusPill, isConnected && styles.statusPillConnected]}
              activeOpacity={0.8}
              onPress={() => setConnectModalVisible(true)}
            >
              <View style={[styles.dot, { backgroundColor: dotColor }]} />
              <Text style={styles.statusText}>{statusText}</Text>
            </TouchableOpacity>
          </View>

          {/* 2. Ô nhập chữ tìm kiếm trực tiếp trên TV */}
          <RealtimeSearchBar />

          {/* 3. Vùng điều khiển trung tâm (Phím 5 chiều hoặc Bàn rê chuột) */}
          <View style={styles.centerZone}>
            <View style={styles.modeSwitch}>
              <TouchableOpacity
                style={[styles.modeBtn, padMode === 'dpad' && styles.modeBtnActive]}
                onPress={() => {
                  void Haptics.selectionAsync();
                  setPadMode('dpad');
                }}
              >
                <Text
                  style={[
                    styles.modeText,
                    padMode === 'dpad' && styles.modeTextActive,
                  ]}
                >
                  Phím bấm
                </Text>
              </TouchableOpacity>

              <TouchableOpacity
                style={[
                  styles.modeBtn,
                  padMode === 'touchpad' && styles.modeBtnActive,
                ]}
                onPress={() => {
                  void Haptics.selectionAsync();
                  setPadMode('touchpad');
                }}
              >
                <Text
                  style={[
                    styles.modeText,
                    padMode === 'touchpad' && styles.modeTextActive,
                  ]}
                >
                  Rê chuột
                </Text>
              </TouchableOpacity>
            </View>

            <View style={styles.padArea}>
              {padMode === 'dpad' ? (
                <View style={styles.dpadCircle}>
                  <TouchableOpacity
                    style={[styles.arrowBtn, styles.arrowVertical]}
                    activeOpacity={0.65}
                    onPress={() => pressRemote('DPAD_UP')}
                  >
                    <Text style={styles.arrowText}>▲</Text>
                  </TouchableOpacity>

                  <View style={styles.dpadMidRow}>
                    <TouchableOpacity
                      style={[styles.arrowBtn, styles.arrowHorizontal]}
                      activeOpacity={0.65}
                      onPress={() => pressRemote('DPAD_LEFT')}
                    >
                      <Text style={styles.arrowText}>◀</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                      activeOpacity={0.82}
                      onPress={() => pressRemote('DPAD_OK', true)}
                      style={styles.okOuter}
                    >
                      <LinearGradient
                        colors={['#38BDF8', '#2563EB']}
                        style={styles.okInner}
                      >
                        <Text style={styles.okLabel}>OK</Text>
                      </LinearGradient>
                    </TouchableOpacity>

                    <TouchableOpacity
                      style={[styles.arrowBtn, styles.arrowHorizontal]}
                      activeOpacity={0.65}
                      onPress={() => pressRemote('DPAD_RIGHT')}
                    >
                      <Text style={styles.arrowText}>▶</Text>
                    </TouchableOpacity>
                  </View>

                  <TouchableOpacity
                    style={[styles.arrowBtn, styles.arrowVertical]}
                    activeOpacity={0.65}
                    onPress={() => pressRemote('DPAD_DOWN')}
                  >
                    <Text style={styles.arrowText}>▼</Text>
                  </TouchableOpacity>
                </View>
              ) : (
                <TouchpadSurface />
              )}
            </View>
          </View>

          {/* 4. Cụm 6 phím chức năng cơ bản nhất của một chiếc điều khiển TV thật */}
          <View style={styles.hardButtonsGrid}>
            <View style={styles.btnRow}>
              <TouchableOpacity
                style={styles.remoteBtn}
                activeOpacity={0.75}
                onPress={() => pressRemote('BACK')}
              >
                <Text style={styles.btnIcon}>←</Text>
                <Text style={styles.btnLabel}>Quay lại</Text>
              </TouchableOpacity>

              <TouchableOpacity
                style={[styles.remoteBtn, styles.homeBtn]}
                activeOpacity={0.8}
                onPress={() => pressRemote('HOME', true)}
              >
                <Text style={[styles.btnIcon, { color: Colors.accentCyan }]}>⌂</Text>
                <Text style={[styles.btnLabel, { fontWeight: '700' }]}>Trang Chủ</Text>
              </TouchableOpacity>

              <TouchableOpacity
                style={styles.remoteBtn}
                activeOpacity={0.75}
                onPress={() => pressRemote('PLAY_PAUSE', true)}
              >
                <Text style={styles.btnIcon}>⏯</Text>
                <Text style={styles.btnLabel}>Phát / Dừng</Text>
              </TouchableOpacity>
            </View>

            <View style={styles.btnRow}>
              <TouchableOpacity
                style={styles.remoteBtn}
                activeOpacity={0.75}
                onPress={() => pressRemote('VOL_DOWN')}
              >
                <Text style={styles.volSign}>−</Text>
                <Text style={styles.btnLabel}>Giảm âm</Text>
              </TouchableOpacity>

              <TouchableOpacity
                style={[
                  styles.remoteBtn,
                  remoteState.isMuted && styles.mutedBtn,
                ]}
                activeOpacity={0.75}
                onPress={() => pressRemote('MUTE_TOGGLE', true)}
              >
                <Text style={styles.btnIcon}>
                  {remoteState.isMuted ? '🔇' : '🔊'}
                </Text>
                <Text style={styles.btnLabel}>
                  {remoteState.isMuted ? 'Đã tắt tiếng' : `${remoteState.volume}%`}
                </Text>
              </TouchableOpacity>

              <TouchableOpacity
                style={styles.remoteBtn}
                activeOpacity={0.75}
                onPress={() => pressRemote('VOL_UP')}
              >
                <Text style={styles.volSign}>+</Text>
                <Text style={styles.btnLabel}>Tăng âm</Text>
              </TouchableOpacity>
            </View>
          </View>

          {/* 5. Hàng mở nhanh kênh bằng Logo trực quan (chỉ gồm logo không rối chữ) */}
          <View style={styles.channelsStrip}>
            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerStyle={styles.channelsContent}
            >
              {QUICK_CHANNELS.map((ch) => (
                <TouchableOpacity
                  key={ch.target}
                  style={styles.channelChip}
                  activeOpacity={0.75}
                  onPress={() => pressRemote('OPEN_APP', true, ch.target)}
                >
                  <BrandLogo brand={ch.brand} size={30} />
                </TouchableOpacity>
              ))}
            </ScrollView>
          </View>
        </View>

        <ConnectionModal
          visible={connectModalVisible}
          onClose={() => setConnectModalVisible(false)}
          status={status}
          currentHost={host}
        />
      </SafeAreaView>
    </LinearGradient>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
  },
  safeArea: {
    flex: 1,
    paddingTop: Platform.OS === 'android' ? (RNStatusBar.currentHeight ?? 10) : 0,
  },
  screen: {
    flex: 1,
    paddingHorizontal: 18,
    paddingBottom: Platform.OS === 'ios' ? 6 : 14,
    justifyContent: 'space-between',
    gap: 12,
  },
  topBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: 4,
  },
  appTitle: {
    color: Colors.textPrimary,
    fontSize: 20,
    fontWeight: '800',
    letterSpacing: 0.3,
  },
  statusPill: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 13,
    paddingVertical: 7,
    borderRadius: 18,
    backgroundColor: 'rgba(255,255,255,0.07)',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.12)',
    gap: 7,
  },
  statusPillConnected: {
    backgroundColor: 'rgba(52, 211, 153, 0.14)',
    borderColor: 'rgba(52, 211, 153, 0.35)',
  },
  dot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  statusText: {
    color: Colors.textPrimary,
    fontSize: 13,
    fontWeight: '700',
  },
  centerZone: {
    flex: 1,
    alignItems: 'center',
    gap: 10,
  },
  modeSwitch: {
    flexDirection: 'row',
    backgroundColor: 'rgba(255,255,255,0.06)',
    borderRadius: 14,
    padding: 3,
    width: 210,
  },
  modeBtn: {
    flex: 1,
    paddingVertical: 6,
    borderRadius: 11,
    alignItems: 'center',
  },
  modeBtnActive: {
    backgroundColor: 'rgba(56, 189, 248, 0.22)',
  },
  modeText: {
    color: Colors.textMuted,
    fontSize: 13,
    fontWeight: '600',
  },
  modeTextActive: {
    color: Colors.textPrimary,
    fontWeight: '700',
  },
  padArea: {
    flex: 1,
    width: '100%',
    alignItems: 'center',
    justifyContent: 'center',
  },
  dpadCircle: {
    width: 224,
    height: 224,
    borderRadius: 112,
    backgroundColor: 'rgba(17, 27, 51, 0.95)',
    borderWidth: 1.5,
    borderColor: 'rgba(255, 255, 255, 0.14)',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 10,
  },
  dpadMidRow: {
    width: '100%',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  arrowBtn: {
    alignItems: 'center',
    justifyContent: 'center',
  },
  arrowVertical: {
    width: 96,
    height: 54,
  },
  arrowHorizontal: {
    width: 54,
    height: 96,
  },
  arrowText: {
    color: Colors.textSecondary,
    fontSize: 19,
  },
  okOuter: {
    width: 86,
    height: 86,
    borderRadius: 43,
    padding: 3,
    backgroundColor: 'rgba(56, 189, 248, 0.25)',
  },
  okInner: {
    flex: 1,
    borderRadius: 40,
    alignItems: 'center',
    justifyContent: 'center',
  },
  okLabel: {
    color: '#FFFFFF',
    fontSize: 20,
    fontWeight: '800',
  },
  hardButtonsGrid: {
    gap: 10,
  },
  btnRow: {
    flexDirection: 'row',
    gap: 10,
  },
  remoteBtn: {
    flex: 1,
    height: 62,
    borderRadius: 18,
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.11)',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 2,
  },
  homeBtn: {
    backgroundColor: 'rgba(56, 189, 248, 0.14)',
    borderColor: 'rgba(56, 189, 248, 0.36)',
  },
  mutedBtn: {
    backgroundColor: Colors.dangerBg,
    borderColor: Colors.danger,
  },
  btnIcon: {
    color: Colors.textPrimary,
    fontSize: 19,
    fontWeight: '700',
  },
  volSign: {
    color: Colors.textPrimary,
    fontSize: 24,
    fontWeight: '700',
    lineHeight: 24,
  },
  btnLabel: {
    color: Colors.textSecondary,
    fontSize: 12,
    fontWeight: '600',
  },
  channelsStrip: {
    paddingTop: 2,
  },
  channelsContent: {
    gap: 10,
    paddingRight: 4,
  },
  channelChip: {
    width: 54,
    height: 54,
    borderRadius: 16,
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.1)',
    alignItems: 'center',
    justifyContent: 'center',
  },
});
