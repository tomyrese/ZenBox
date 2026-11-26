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
  const [scanning, setScanning] = useState(false);
  const [connecting, setConnecting] = useState(false);
  const [showManualIp, setShowManualIp] = useState(false);
  const [ipInput, setIpInput] = useState(currentHost || '');
  const [devices, setDevices] = useState<DiscoveredZenBoxDevice[]>([]);
  const [message, setMessage] = useState('');

  useEffect(() => {
    if (visible) {
      setMessage('');
      if (currentHost) setIpInput(currentHost);
    }
  }, [visible, currentHost]);

  const connectToIp = async (ip: string) => {
    const trimmed = ip.trim();
    if (!trimmed) return;

    if (trimmed.startsWith('26.')) {
      setMessage('Vui lòng nhập IP Wi-Fi (192.168.x.x), không dùng số 26.x.x.x.');
      return;
    }

    setMessage('');
    setConnecting(true);
    const ok = await zenSocket.connectAndVerify(trimmed);
    setConnecting(false);

    if (ok) {
      void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
      onClose();
    } else {
      void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
      setMessage(
        'Chưa nối được TV. Hãy kiểm tra ZenBox đang mở trên máy tính và dùng chung Wi-Fi.'
      );
    }
  };

  const handleAutoFind = async () => {
    if (scanning) return;
    void Haptics.selectionAsync();
    setMessage('');
    setScanning(true);
    setDevices([]);

    try {
      const found = await zenSocket.scanLocalNetwork();
      setDevices(found);
      if (found.length === 1) {
        await connectToIp(found[0].ip);
      } else if (found.length === 0) {
        setMessage(
          'Không tìm thấy TV. Hãy mở ZenBox trên máy tính rồi thử lại (hoặc nhập số IP thủ công).'
        );
      }
    } finally {
      setScanning(false);
    }
  };

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <View style={styles.backdrop}>
        <View style={styles.card}>
          <View style={styles.topRow}>
            <Text style={styles.title}>Kết nối TV</Text>
            <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
              <Text style={styles.closeText}>✕</Text>
            </TouchableOpacity>
          </View>

          <TouchableOpacity
            style={styles.primaryBtn}
            activeOpacity={0.82}
            disabled={scanning || connecting}
            onPress={() => void handleAutoFind()}
          >
            {scanning || connecting ? (
              <View style={styles.loadingRow}>
                <ActivityIndicator size="small" color="#FFFFFF" />
                <Text style={styles.primaryBtnText}>Đang tìm TV trên Wi-Fi...</Text>
              </View>
            ) : (
              <Text style={styles.primaryBtnText}>Tự động tìm & Kết nối TV</Text>
            )}
          </TouchableOpacity>

          {devices.length > 1 && (
            <View style={styles.deviceList}>
              {devices.map((d) => (
                <TouchableOpacity
                  key={d.ip}
                  style={styles.deviceItem}
                  onPress={() => void connectToIp(d.ip)}
                >
                  <Text style={styles.deviceName}>📺 {d.serverName}</Text>
                  <Text style={styles.deviceAction}>Kết nối</Text>
                </TouchableOpacity>
              ))}
            </View>
          )}

          {message.length > 0 && <Text style={styles.msgText}>{message}</Text>}

          <TouchableOpacity
            style={styles.manualToggle}
            onPress={() => setShowManualIp((v) => !v)}
          >
            <Text style={styles.manualToggleText}>
              {showManualIp ? 'Ẩn nhập IP thủ công' : 'Nhập địa chỉ IP thủ công'}
            </Text>
          </TouchableOpacity>

          {showManualIp && (
            <View style={styles.manualRow}>
              <TextInput
                value={ipInput}
                onChangeText={setIpInput}
                placeholder="VD: 192.168.2.35"
                placeholderTextColor={Colors.textMuted}
                keyboardType="numbers-and-punctuation"
                autoCapitalize="none"
                style={styles.ipInput}
              />
              <TouchableOpacity
                style={styles.goBtn}
                disabled={connecting}
                onPress={() => void connectToIp(ipInput)}
              >
                <Text style={styles.goBtnText}>Nối</Text>
              </TouchableOpacity>
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
              <Text style={styles.disconnectText}>Ngắt kết nối</Text>
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
    backgroundColor: 'rgba(3, 6, 15, 0.8)',
    justifyContent: 'center',
    padding: 22,
  },
  card: {
    backgroundColor: '#0E172C',
    borderRadius: 24,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.14)',
    padding: 20,
    gap: 14,
  },
  topRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  title: {
    color: Colors.textPrimary,
    fontSize: 19,
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
  primaryBtn: {
    height: 52,
    borderRadius: 16,
    backgroundColor: Colors.accentBlue,
    alignItems: 'center',
    justifyContent: 'center',
  },
  loadingRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  primaryBtnText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '700',
  },
  deviceList: {
    gap: 8,
  },
  deviceItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 13,
    borderRadius: 14,
    backgroundColor: 'rgba(255,255,255,0.06)',
  },
  deviceName: {
    color: Colors.textPrimary,
    fontSize: 15,
    fontWeight: '600',
  },
  deviceAction: {
    color: Colors.accentCyan,
    fontSize: 14,
    fontWeight: '700',
  },
  msgText: {
    color: '#FCA5A5',
    fontSize: 13,
    lineHeight: 19,
    textAlign: 'center',
  },
  manualToggle: {
    alignItems: 'center',
    paddingVertical: 4,
  },
  manualToggleText: {
    color: Colors.textSecondary,
    fontSize: 13,
    fontWeight: '600',
  },
  manualRow: {
    flexDirection: 'row',
    gap: 8,
  },
  ipInput: {
    flex: 1,
    height: 46,
    borderRadius: 13,
    backgroundColor: 'rgba(255,255,255,0.06)',
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    paddingHorizontal: 14,
    color: Colors.textPrimary,
    fontSize: 15,
  },
  goBtn: {
    paddingHorizontal: 18,
    height: 46,
    borderRadius: 13,
    backgroundColor: 'rgba(56,189,248,0.22)',
    borderWidth: 1,
    borderColor: Colors.accentCyan,
    alignItems: 'center',
    justifyContent: 'center',
  },
  goBtnText: {
    color: Colors.textPrimary,
    fontSize: 14,
    fontWeight: '700',
  },
  disconnectBtn: {
    alignItems: 'center',
    paddingTop: 2,
  },
  disconnectText: {
    color: Colors.danger,
    fontSize: 13,
    fontWeight: '700',
  },
});
