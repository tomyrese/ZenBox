import React, { useEffect, useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  SafeAreaView,
  StatusBar as RNStatusBar,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { StatusBar } from 'expo-status-bar';
import { LinearGradient } from 'expo-linear-gradient';
import * as Haptics from 'expo-haptics';
import Svg, { Defs, LinearGradient as SvgGrad, Path, Polygon, Stop } from 'react-native-svg';

import { Colors } from './src/theme/colors';
import {
  ConnectionStatus,
  DEFAULT_REMOTE_STATE,
  RemoteState,
  RemoteTab,
} from './src/types/remote';
import { zenSocket } from './src/services/ZenBoxSocket';
import { RealtimeSearchBar } from './src/components/RealtimeSearchBar';
import { RemoteDpadTab } from './src/components/RemoteDpadTab';
import { TouchpadTab } from './src/components/TouchpadTab';
import { AppsGridTab } from './src/components/AppsGridTab';
import { TvSettingsTab } from './src/components/TvSettingsTab';
import { ConnectionModal } from './src/components/ConnectionModal';

const TABS: { key: RemoteTab; icon: string; label: string }[] = [
  { key: 'dpad', icon: '❖', label: 'Điều khiển' },
  { key: 'touchpad', icon: '◰', label: 'Bàn rê' },
  { key: 'apps', icon: '⊞', label: 'Ứng dụng' },
  { key: 'settings', icon: '☰', label: 'Tiện ích TV' },
];

export default function App() {
  const [status, setStatus] = useState<ConnectionStatus>('disconnected');
  const [host, setHost] = useState<string>('');
  const [remoteState, setRemoteState] = useState<RemoteState>(DEFAULT_REMOTE_STATE);
  const [activeTab, setActiveTab] = useState<RemoteTab>('dpad');
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

  const switchTab = (tab: RemoteTab) => {
    void Haptics.selectionAsync();
    setActiveTab(tab);
  };

  const isConnected = status === 'connected';
  const clientCount = remoteState.connectedClients ?? 1;
  const statusLabel =
    status === 'connected'
      ? clientCount > 1
        ? `${host} • ${clientCount} ĐT`
        : host
      : status === 'connecting'
      ? 'Đang nối TV...'
      : 'Bấm kết nối TV';

  const statusDotColor =
    status === 'connected'
      ? Colors.success
      : status === 'connecting'
      ? Colors.warning
      : Colors.danger;

  return (
    <LinearGradient
      colors={[Colors.bgDeep, Colors.bgMid, '#060C1A']}
      style={styles.rootGradient}
    >
      <StatusBar style="light" />
      <SafeAreaView style={styles.safeArea}>
        <KeyboardAvoidingView
          style={styles.flexOne}
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        >
          {/* Thanh tiêu đề thương hiệu ZenBox & Trạng thái kết nối đa thiết bị */}
          <View style={styles.header}>
            <View style={styles.brandRow}>
              <Svg width={30} height={30} viewBox="0 0 48 48">
                <Defs>
                  <SvgGrad id="zbLogo" x1="0" y1="0" x2="1" y2="1">
                    <Stop offset="0" stopColor="#38BDF8" />
                    <Stop offset="1" stopColor="#2563EB" />
                  </SvgGrad>
                </Defs>
                <Path
                  d="M24 4L41 14V34L24 44L7 34V14L24 4Z"
                  fill="url(#zbLogo)"
                />
                <Polygon points="20,16 32,24 20,32" fill="#FFFFFF" />
              </Svg>
              <View>
                <Text style={styles.brandTitle}>ZenBox Remote</Text>
                <Text style={styles.brandSub}>
                  {isConnected && remoteState.serverName
                    ? `TV: ${remoteState.serverName}`
                    : 'Điều khiển đa thiết bị'}
                </Text>
              </View>
            </View>

            <TouchableOpacity
              style={[
                styles.connectionPill,
                isConnected && styles.connectionPillActive,
              ]}
              activeOpacity={0.8}
              onPress={() => setConnectModalVisible(true)}
            >
              <View
                style={[styles.statusDot, { backgroundColor: statusDotColor }]}
              />
              <Text style={styles.connectionText} numberOfLines={1}>
                {statusLabel}
              </Text>
            </TouchableOpacity>
          </View>

          {/* Thanh cảnh báo nhanh nếu chưa kết nối để người dùng biết ngay vì sao cần kết nối trước khi bấm */}
          {!isConnected && (
            <TouchableOpacity
              style={styles.offlineBanner}
              activeOpacity={0.85}
              onPress={() => setConnectModalVisible(true)}
            >
              <Text style={styles.offlineBannerText}>
                ⚠ Chưa nối với ZenBox PC — Chạm vào đây để quét Wi-Fi hoặc nhập IP kết nối
              </Text>
            </TouchableOpacity>
          )}

          {/* Thanh Tìm Kiếm Realtime luôn sẵn sàng ở mọi màn hình */}
          <RealtimeSearchBar
            connected={isConnected}
            activeSection={remoteState.activeSection}
          />

          {/* Nội dung chính theo Tab */}
          <View style={styles.mainContent}>
            {activeTab === 'dpad' && (
              <RemoteDpadTab state={remoteState} connected={isConnected} />
            )}
            {activeTab === 'touchpad' && <TouchpadTab />}
            {activeTab === 'apps' && (
              <AppsGridTab activeSection={remoteState.activeSection} />
            )}
            {activeTab === 'settings' && <TvSettingsTab state={remoteState} />}
          </View>

          {/* Thanh điều hướng kính pha lê dưới đáy */}
          <View style={styles.bottomBarOuter}>
            <View style={styles.bottomBar}>
              {TABS.map((tab) => {
                const selected = activeTab === tab.key;
                return (
                  <TouchableOpacity
                    key={tab.key}
                    style={[styles.tabItem, selected && styles.tabItemSelected]}
                    activeOpacity={0.78}
                    onPress={() => switchTab(tab.key)}
                  >
                    <Text
                      style={[
                        styles.tabIcon,
                        selected && styles.tabIconSelected,
                      ]}
                    >
                      {tab.icon}
                    </Text>
                    <Text
                      style={[
                        styles.tabLabel,
                        selected && styles.tabLabelSelected,
                      ]}
                    >
                      {tab.label}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>
          </View>
        </KeyboardAvoidingView>

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
  rootGradient: {
    flex: 1,
  },
  safeArea: {
    flex: 1,
    paddingTop: Platform.OS === 'android' ? (RNStatusBar.currentHeight ?? 12) : 0,
  },
  flexOne: {
    flex: 1,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingTop: 8,
    paddingBottom: 6,
  },
  brandRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 9,
  },
  brandTitle: {
    color: Colors.textPrimary,
    fontSize: 17,
    fontWeight: '800',
    letterSpacing: 0.2,
  },
  brandSub: {
    color: Colors.textMuted,
    fontSize: 11,
    fontWeight: '600',
  },
  connectionPill: {
    flexDirection: 'row',
    alignItems: 'center',
    maxWidth: 185,
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: 16,
    backgroundColor: 'rgba(255,255,255,0.06)',
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    gap: 6,
  },
  connectionPillActive: {
    backgroundColor: Colors.successBg,
    borderColor: 'rgba(52, 211, 153, 0.36)',
  },
  statusDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  connectionText: {
    color: Colors.textPrimary,
    fontSize: 12,
    fontWeight: '700',
  },
  offlineBanner: {
    marginHorizontal: 16,
    marginBottom: 4,
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 12,
    backgroundColor: Colors.warningBg,
    borderWidth: 1,
    borderColor: 'rgba(251, 191, 36, 0.38)',
  },
  offlineBannerText: {
    color: '#FDE68A',
    fontSize: 12,
    fontWeight: '700',
    textAlign: 'center',
  },
  mainContent: {
    flex: 1,
  },
  bottomBarOuter: {
    paddingHorizontal: 14,
    paddingBottom: Platform.OS === 'ios' ? 4 : 12,
    paddingTop: 4,
  },
  bottomBar: {
    flexDirection: 'row',
    backgroundColor: 'rgba(13, 22, 43, 0.94)',
    borderRadius: 22,
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    padding: 5,
    gap: 4,
  },
  tabItem: {
    flex: 1,
    paddingVertical: 8,
    borderRadius: 17,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 2,
  },
  tabItemSelected: {
    backgroundColor: 'rgba(56, 189, 248, 0.18)',
  },
  tabIcon: {
    color: Colors.textMuted,
    fontSize: 16,
    fontWeight: '700',
  },
  tabIconSelected: {
    color: Colors.accentCyan,
  },
  tabLabel: {
    color: Colors.textMuted,
    fontSize: 11,
    fontWeight: '600',
  },
  tabLabelSelected: {
    color: Colors.textPrimary,
    fontWeight: '700',
  },
});
