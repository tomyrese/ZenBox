import React, { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Modal,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import * as Haptics from 'expo-haptics';
import { Colors } from '../theme/colors';
import { ConnectionStatus, DiscoveredZenBoxDevice } from '../types/remote';
import { zenSocket } from '../services/ZenBoxSocket';

interface ConnectionModalProps {
  visible: boolean;
  onClose: () => void;
  status: ConnectionStatus;
  currentHost: string;
}

export const ConnectionModal: React.FC<ConnectionModalProps> = ({
  visible,
  onClose,
  status,
  currentHost,
}) => {
  const [ipInput, setIpInput] = useState(currentHost || '');
  const [phoneIp, setPhoneIp] = useState<string | null>(null);
  const [recentHosts, setRecentHosts] = useState<string[]>([]);
  const [connecting, setConnecting] = useState(false);
  const [scanning, setScanning] = useState(false);
  const [scanProgress, setScanProgress] = useState(0);
  const [discoveredDevices, setDiscoveredDevices] = useState<DiscoveredZenBoxDevice[]>([]);
  const [errorMessage, setErrorMessage] = useState<string>('');

  useEffect(() => {
    if (visible) {
      setErrorMessage('');
      void zenSocket.getRecentHosts().then(setRecentHosts);
      void zenSocket.getPhoneWifiIp().then((detectedPhoneIp) => {
        setPhoneIp(detectedPhoneIp);
        if (!currentHost && !ipInput) {
          const expoIp = zenSocket.getExpoHostIp();
          if (expoIp && !expoIp.startsWith('26.')) {
            setIpInput(expoIp);
          } else if (detectedPhoneIp) {
            const parts = detectedPhoneIp.split('.');
            if (parts.length === 4) {
              setIpInput(`${parts[0]}.${parts[1]}.${parts[2]}.`);
            }
          }
        } else if (currentHost) {
          setIpInput(currentHost);
        }
      });
    }
  }, [visible, currentHost]);

  const handleConnect = async (targetHost: string) => {
    const trimmed = targetHost.trim();
    if (!trimmed) {
      setErrorMessage('Vui lòng nhập địa chỉ IP Wi-Fi của máy tính (VD: 192.168.2.35).');
      return;
    }

    if (trimmed.startsWith('26.')) {
      setErrorMessage(
        'Địa chỉ 26.x.x.x là mạng ảo Radmin VPN nên điện thoại Wi-Fi không kết nối được! Hãy nhập địa chỉ Wi-Fi thật 192.168.x.x của máy tính.'
      );
      return;
    }

    setErrorMessage('');
    setConnecting(true);
    void Haptics.selectionAsync();

    const ok = await zenSocket.connectAndVerify(trimmed);
    setConnecting(false);

    if (ok) {
      void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
      onClose();
    } else {
      void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
      setErrorMessage(
        `Không thể kết nối tới ${trimmed}:8989.\n• Hãy mở bản ZenBox.exe mới nhất trên máy tính.\n• Bấm chọn "Yes" (hoặc nút "Mở cổng Wi-Fi" trong Cài đặt → Thư viện & Hệ thống) để cho phép qua Tường lửa Windows.`
      );
    }
  };

  const handleScanWifi = async () => {
    if (scanning) return;
    void Haptics.selectionAsync();
    setErrorMessage('');
    setScanning(true);
    setScanProgress(0);
    setDiscoveredDevices([]);

    try {
      const found = await zenSocket.scanLocalNetwork(ipInput, (pct) => {
        setScanProgress(pct);
      });
      setDiscoveredDevices(found);

      if (found.length === 1) {
        setIpInput(found[0].ip);
        // Tự động nối ngay khi tìm thấy duy nhất 1 máy chủ ZenBox trong nhà!
        await handleConnect(found[0].ip);
      } else if (found.length === 0) {
        setErrorMessage(
          'Chưa tìm thấy máy tính ZenBox trên Wi-Fi. Hãy mở ZenBox trên máy tính và bấm nút "Mở cổng Wi-Fi" trong mục Cài đặt → Thư viện & Hệ thống để cho phép qua Tường lửa Windows.'
        );
      }
    } finally {
      setScanning(false);
    }
  };

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={onClose}
    >
      <View style={styles.backdrop}>
        <View style={styles.sheet}>
          <View style={styles.headerRow}>
            <Text style={styles.title}>Kết nối với ZenBox TV</Text>
            <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
              <Text style={styles.closeText}>✕</Text>
            </TouchableOpacity>
          </View>

          <Text style={styles.guideText}>
            Hỗ trợ <Text style={styles.highlight}>nhiều điện thoại kết nối cùng lúc</Text>. Xem IP Wi-Fi thật ngay góc dưới màn hình chính ZenBox hoặc tại{' '}
            <Text style={styles.highlight}>Cài đặt → Thư viện & Hệ thống</Text>.
          </Text>

          {phoneIp && (
            <View style={styles.phoneIpBadge}>
              <Text style={styles.phoneIpText}>
                📱 Mạng Wi-Fi điện thoại hiện tại: {phoneIp}
              </Text>
            </View>
          )}

          <View style={styles.inputRow}>
            <TextInput
              value={ipInput}
              onChangeText={(txt) => {
                setIpInput(txt);
                if (errorMessage) setErrorMessage('');
              }}
              placeholder="Ví dụ: 192.168.2.35"
              placeholderTextColor={Colors.textMuted}
              keyboardType="numbers-and-punctuation"
              autoCapitalize="none"
              autoCorrect={false}
              style={styles.ipInput}
            />
            <TouchableOpacity
              style={[styles.connectBtn, connecting && { opacity: 0.7 }]}
              activeOpacity={0.82}
              disabled={connecting}
              onPress={() => void handleConnect(ipInput)}
            >
              {connecting ? (
                <ActivityIndicator size="small" color="#FFFFFF" />
              ) : (
                <Text style={styles.connectBtnText}>Kết nối</Text>
              )}
            </TouchableOpacity>
          </View>

          {/* Tự động quét tìm TV trong đúng dải mạng Wi-Fi của điện thoại */}
          <TouchableOpacity
            style={styles.scanBtn}
            activeOpacity={0.8}
            onPress={() => void handleScanWifi()}
            disabled={scanning || connecting}
          >
            {scanning ? (
              <View style={styles.scanLoadingRow}>
                <ActivityIndicator size="small" color={Colors.accentCyan} />
                <Text style={styles.scanBtnText}>
                  Đang quét dải mạng Wi-Fi... ({scanProgress}%)
                </Text>
              </View>
            ) : (
              <Text style={styles.scanBtnText}>
                🔍 Tự động dò tìm TV trong mạng Wi-Fi
              </Text>
            )}
          </TouchableOpacity>

          {errorMessage.length > 0 && (
            <View style={styles.errorBox}>
              <Text style={styles.errorText}>{errorMessage}</Text>
            </View>
          )}

          {discoveredDevices.length > 0 && (
            <View style={styles.listSection}>
              <Text style={styles.listTitle}>Thiết bị ZenBox tìm thấy trên Wi-Fi:</Text>
              {discoveredDevices.map((dev) => (
                <TouchableOpacity
                  key={dev.ip}
                  style={styles.deviceRow}
                  onPress={() => void handleConnect(dev.ip)}
                >
                  <View>
                    <Text style={styles.deviceIp}>
                      📺 {dev.serverName} ({dev.ip})
                    </Text>
                    <Text style={styles.deviceSub}>
                      Đang có {dev.connectedClients} điện thoại kết nối
                    </Text>
                  </View>
                  <Text style={styles.deviceAction}>Chạm để nối →</Text>
                </TouchableOpacity>
              ))}
            </View>
          )}

          {recentHosts.length > 0 && (
            <View style={styles.listSection}>
              <Text style={styles.listTitle}>Đã kết nối gần đây:</Text>
              {recentHosts.map((host) => (
                <TouchableOpacity
                  key={host}
                  style={styles.deviceRow}
                  onPress={() => void handleConnect(host)}
                >
                  <Text style={styles.deviceIp}>{host}</Text>
                  <Text style={styles.deviceAction}>Kết nối lại</Text>
                </TouchableOpacity>
              ))}
            </View>
          )}

          {status === 'connected' && (
            <TouchableOpacity
              style={styles.disconnectBtn}
              onPress={() => {
                zenSocket.disconnect();
                onClose();
              }}
            >
              <Text style={styles.disconnectText}>Ngắt kết nối điện thoại này</Text>
            </TouchableOpacity>
          )}
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(3, 6, 15, 0.82)',
    justifyContent: 'center',
    padding: 18,
  },
  sheet: {
    backgroundColor: '#0D162B',
    borderRadius: 24,
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.35)',
    padding: 20,
    gap: 12,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  title: {
    color: Colors.textPrimary,
    fontSize: 18,
    fontWeight: '800',
  },
  closeBtn: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: 'rgba(255,255,255,0.08)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  closeText: {
    color: Colors.textSecondary,
    fontSize: 14,
    fontWeight: '700',
  },
  guideText: {
    color: Colors.textSecondary,
    fontSize: 13,
    lineHeight: 19,
  },
  highlight: {
    color: Colors.accentCyan,
    fontWeight: '700',
  },
  phoneIpBadge: {
    backgroundColor: 'rgba(56, 189, 248, 0.09)',
    borderRadius: 10,
    paddingHorizontal: 11,
    paddingVertical: 7,
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.2)',
  },
  phoneIpText: {
    color: Colors.accentCyan,
    fontSize: 12,
    fontWeight: '600',
  },
  inputRow: {
    flexDirection: 'row',
    gap: 10,
  },
  ipInput: {
    flex: 1,
    height: 48,
    borderRadius: 14,
    backgroundColor: 'rgba(255,255,255,0.06)',
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    paddingHorizontal: 14,
    color: Colors.textPrimary,
    fontSize: 15,
    fontWeight: '600',
  },
  connectBtn: {
    minWidth: 88,
    paddingHorizontal: 16,
    height: 48,
    borderRadius: 14,
    backgroundColor: Colors.accentBlue,
    alignItems: 'center',
    justifyContent: 'center',
  },
  connectBtnText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '700',
  },
  scanBtn: {
    height: 46,
    borderRadius: 14,
    backgroundColor: 'rgba(56, 189, 248, 0.14)',
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.38)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  scanLoadingRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  scanBtnText: {
    color: Colors.accentCyan,
    fontSize: 13,
    fontWeight: '700',
  },
  errorBox: {
    backgroundColor: Colors.dangerBg,
    borderRadius: 12,
    padding: 11,
    borderWidth: 1,
    borderColor: 'rgba(248, 113, 113, 0.4)',
  },
  errorText: {
    color: '#FCA5A5',
    fontSize: 12.5,
    lineHeight: 18,
  },
  listSection: {
    gap: 7,
  },
  listTitle: {
    color: Colors.textMuted,
    fontSize: 12,
    fontWeight: '700',
  },
  deviceRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: 'rgba(255,255,255,0.05)',
    borderRadius: 12,
    paddingHorizontal: 13,
    paddingVertical: 10,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
  },
  deviceIp: {
    color: Colors.textPrimary,
    fontSize: 14,
    fontWeight: '700',
  },
  deviceSub: {
    color: Colors.textSecondary,
    fontSize: 11,
    marginTop: 2,
  },
  deviceAction: {
    color: Colors.accentCyan,
    fontSize: 12,
    fontWeight: '700',
  },
  disconnectBtn: {
    paddingVertical: 8,
    alignItems: 'center',
  },
  disconnectText: {
    color: Colors.danger,
    fontSize: 13,
    fontWeight: '700',
  },
});
