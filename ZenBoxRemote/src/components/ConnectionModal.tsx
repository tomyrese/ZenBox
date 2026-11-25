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
import { ConnectionStatus } from '../types/remote';
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
  const [ipInput, setIpInput] = useState(currentHost || '192.168.1.');
  const [recentHosts, setRecentHosts] = useState<string[]>([]);
  const [scanning, setScanning] = useState(false);
  const [scanProgress, setScanProgress] = useState(0);
  const [discoveredIps, setDiscoveredIps] = useState<string[]>([]);

  useEffect(() => {
    if (visible) {
      if (currentHost) {
        setIpInput(currentHost);
      }
      void zenSocket.getRecentHosts().then(setRecentHosts);
    }
  }, [visible, currentHost]);

  const handleConnect = (targetHost: string) => {
    const trimmed = targetHost.trim();
    if (!trimmed) return;
    void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);
    zenSocket.connect(trimmed);
    onClose();
  };

  const handleScanWifi = async () => {
    if (scanning) return;
    void Haptics.selectionAsync();
    setScanning(true);
    setScanProgress(0);
    setDiscoveredIps([]);

    try {
      const found = await zenSocket.scanLocalNetwork(ipInput, (pct) => {
        setScanProgress(pct);
      });
      setDiscoveredIps(found);
      if (found.length === 1) {
        setIpInput(found[0]);
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
            Đảm bảo điện thoại và máy tính chạy ZenBox đang dùng chung mạng Wi-Fi. Xem địa chỉ IP trên TV tại mục{' '}
            <Text style={{ color: Colors.accentCyan, fontWeight: '700' }}>
              Cài đặt → Thư viện & Hệ thống
            </Text>
            .
          </Text>

          <View style={styles.inputRow}>
            <TextInput
              value={ipInput}
              onChangeText={setIpInput}
              placeholder="Ví dụ: 192.168.1.15"
              placeholderTextColor={Colors.textMuted}
              keyboardType="numbers-and-punctuation"
              autoCapitalize="none"
              autoCorrect={false}
              style={styles.ipInput}
            />
            <TouchableOpacity
              style={styles.connectBtn}
              activeOpacity={0.82}
              onPress={() => handleConnect(ipInput)}
            >
              <Text style={styles.connectBtnText}>Kết nối</Text>
            </TouchableOpacity>
          </View>

          {/* Tự động quét tìm TV trong mạng Wi-Fi */}
          <TouchableOpacity
            style={styles.scanBtn}
            activeOpacity={0.8}
            onPress={handleScanWifi}
            disabled={scanning}
          >
            {scanning ? (
              <View style={styles.scanLoadingRow}>
                <ActivityIndicator size="small" color={Colors.accentCyan} />
                <Text style={styles.scanBtnText}>
                  Đang dò tìm ZenBox trên Wi-Fi... ({scanProgress}%)
                </Text>
              </View>
            ) : (
              <Text style={styles.scanBtnText}>
                🔍 Tự động dò tìm TV trong mạng Wi-Fi
              </Text>
            )}
          </TouchableOpacity>

          {discoveredIps.length > 0 && (
            <View style={styles.listSection}>
              <Text style={styles.listTitle}>Thiết bị ZenBox vừa tìm thấy:</Text>
              {discoveredIps.map((ip) => (
                <TouchableOpacity
                  key={ip}
                  style={styles.deviceRow}
                  onPress={() => handleConnect(ip)}
                >
                  <Text style={styles.deviceIp}>📺 ZenBox PC ({ip}:8989)</Text>
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
                  onPress={() => handleConnect(host)}
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
              <Text style={styles.disconnectText}>Ngắt kết nối hiện tại</Text>
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
    backgroundColor: 'rgba(3, 6, 15, 0.78)',
    justifyContent: 'center',
    padding: 18,
  },
  sheet: {
    backgroundColor: '#0D162B',
    borderRadius: 24,
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.32)',
    padding: 20,
    gap: 14,
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
    paddingHorizontal: 18,
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
    backgroundColor: 'rgba(56, 189, 248, 0.12)',
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.35)',
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
    paddingVertical: 11,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
  },
  deviceIp: {
    color: Colors.textPrimary,
    fontSize: 14,
    fontWeight: '600',
  },
  deviceAction: {
    color: Colors.accentCyan,
    fontSize: 12,
    fontWeight: '700',
  },
  disconnectBtn: {
    paddingVertical: 10,
    alignItems: 'center',
  },
  disconnectText: {
    color: Colors.danger,
    fontSize: 13,
    fontWeight: '700',
  },
});
