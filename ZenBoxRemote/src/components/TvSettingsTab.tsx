import React from 'react';
import {
  Alert,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import * as Haptics from 'expo-haptics';
import { Colors } from '../theme/colors';
import { RemoteState } from '../types/remote';
import { zenSocket } from '../services/ZenBoxSocket';

interface TvSettingsTabProps {
  state: RemoteState;
}

const AUDIO_PRESETS = [
  { key: 'Balanced', title: 'Chuẩn cân bằng', desc: 'Trung thực nguyên bản' },
  { key: 'CinemaSurround', title: 'Rạp chiếu phim', desc: 'Trầm sâu, âm cao sắc nét' },
  { key: 'ClearVoice', title: 'Rõ lời thoại', desc: 'Nổi bật giọng nói & thời sự' },
  { key: 'WarmBolero', title: 'Nhạc trữ tình ấm', desc: 'Âm trung ngọt, êm dịu' },
  { key: 'BassBoost', title: 'Tăng âm trầm', desc: 'Đẩy mạnh dải trầm sôi động' },
  { key: 'NightSoft', title: 'Ban đêm dịu êm', desc: 'Giảm tiếng động lớn đột ngột' },
];

const EYECARE_MODES = [
  { key: 'Off', label: 'Màu chuẩn' },
  { key: 'WarmSoft', label: 'Ấm dịu nhẹ' },
  { key: 'DeepNight', label: 'Ấm sâu ban đêm' },
];

const SLEEP_OPTIONS = [
  { minutes: 0, label: 'Tắt hẹn giờ' },
  { minutes: 15, label: '15 phút' },
  { minutes: 30, label: '30 phút' },
  { minutes: 60, label: '60 phút' },
  { minutes: 90, label: '90 phút' },
];

export const TvSettingsTab: React.FC<TvSettingsTabProps> = ({ state }) => {
  const selectAudioPreset = (presetKey: string) => {
    void Haptics.selectionAsync();
    zenSocket.sendCommand({
      action: 'SET_AUDIO_PRESET',
      target: presetKey,
    });
  };

  const selectEyeCare = (modeKey: string) => {
    void Haptics.selectionAsync();
    zenSocket.sendCommand({
      action: 'SET_EYECARE',
      target: modeKey,
    });
  };

  const selectSleepTimer = (minutes: number) => {
    void Haptics.selectionAsync();
    zenSocket.sendCommand({
      action: 'SET_SLEEP_TIMER',
      value: minutes,
    });
  };

  const confirmExitZenBox = () => {
    Alert.alert(
      'Đóng ZenBox trên TV?',
      'Bạn có chắc muốn thoát ứng dụng ZenBox đang chạy trên máy tính / TV không?',
      [
        { text: 'Hủy', style: 'cancel' },
        {
          text: 'Thoát ZenBox',
          style: 'destructive',
          onPress: () => {
            void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Warning);
            zenSocket.sendCommand({ action: 'EXIT_APP' });
          },
        },
      ]
    );
  };

  return (
    <ScrollView
      contentContainerStyle={styles.container}
      showsVerticalScrollIndicator={false}
    >
      {/* Bộ cân bằng âm thanh 10 dải tần */}
      <View style={styles.card}>
        <Text style={styles.cardTitle}>Chế độ Âm thanh EQ 10 dải tần</Text>
        <View style={styles.presetGrid}>
          {AUDIO_PRESETS.map((p) => {
            const active = state.audioPreset === p.key;
            return (
              <TouchableOpacity
                key={p.key}
                style={[styles.presetBtn, active && styles.presetBtnActive]}
                activeOpacity={0.78}
                onPress={() => selectAudioPreset(p.key)}
              >
                <Text style={[styles.presetTitle, active && styles.presetTitleActive]}>
                  {p.title}
                </Text>
                <Text style={styles.presetDesc} numberOfLines={1}>
                  {p.desc}
                </Text>
              </TouchableOpacity>
            );
          })}
        </View>
      </View>

      {/* Bảo vệ mắt ban đêm */}
      <View style={styles.card}>
        <Text style={styles.cardTitle}>Lọc ánh sáng xanh bảo vệ mắt ban đêm</Text>
        <View style={styles.rowChips}>
          {EYECARE_MODES.map((m) => {
            const active = state.eyeCare === m.key;
            return (
              <TouchableOpacity
                key={m.key}
                style={[styles.choiceChip, active && styles.choiceChipActive]}
                activeOpacity={0.78}
                onPress={() => selectEyeCare(m.key)}
              >
                <Text style={[styles.choiceText, active && styles.choiceTextActive]}>
                  {m.label}
                </Text>
              </TouchableOpacity>
            );
          })}
        </View>
      </View>

      {/* Hẹn giờ tự nghỉ */}
      <View style={styles.card}>
        <View style={styles.cardHeaderRow}>
          <Text style={styles.cardTitle}>Hẹn giờ tự nghỉ (Sleep Timer)</Text>
          <Text style={styles.sleepStatus}>{state.sleepTimer}</Text>
        </View>
        <View style={styles.rowWrap}>
          {SLEEP_OPTIONS.map((s) => (
            <TouchableOpacity
              key={s.minutes}
              style={styles.sleepChip}
              activeOpacity={0.78}
              onPress={() => selectSleepTimer(s.minutes)}
            >
              <Text style={styles.choiceText}>{s.label}</Text>
            </TouchableOpacity>
          ))}
        </View>
      </View>

      {/* Thoát ứng dụng ZenBox */}
      <TouchableOpacity
        style={styles.powerBtn}
        activeOpacity={0.8}
        onPress={confirmExitZenBox}
      >
        <Text style={styles.powerBtnText}>⏻ Đóng ứng dụng ZenBox trên TV</Text>
      </TouchableOpacity>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: 16,
    paddingBottom: 28,
    gap: 14,
  },
  card: {
    backgroundColor: Colors.glassFill,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    padding: 14,
    gap: 11,
  },
  cardHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  cardTitle: {
    color: Colors.textPrimary,
    fontSize: 14,
    fontWeight: '700',
  },
  sleepStatus: {
    color: Colors.accentCyan,
    fontSize: 12,
    fontWeight: '700',
  },
  presetGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 9,
  },
  presetBtn: {
    width: '48.4%',
    borderRadius: 14,
    padding: 11,
    backgroundColor: 'rgba(255,255,255,0.04)',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
    gap: 3,
  },
  presetBtnActive: {
    backgroundColor: 'rgba(56, 189, 248, 0.2)',
    borderColor: Colors.accentCyan,
  },
  presetTitle: {
    color: Colors.textPrimary,
    fontSize: 13,
    fontWeight: '700',
  },
  presetTitleActive: {
    color: '#FFFFFF',
  },
  presetDesc: {
    color: Colors.textSecondary,
    fontSize: 11,
  },
  rowChips: {
    flexDirection: 'row',
    gap: 8,
  },
  choiceChip: {
    flex: 1,
    paddingVertical: 11,
    paddingHorizontal: 8,
    borderRadius: 13,
    backgroundColor: 'rgba(255,255,255,0.05)',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.09)',
    alignItems: 'center',
  },
  choiceChipActive: {
    backgroundColor: 'rgba(56, 189, 248, 0.22)',
    borderColor: Colors.accentCyan,
  },
  choiceText: {
    color: Colors.textSecondary,
    fontSize: 12,
    fontWeight: '700',
  },
  choiceTextActive: {
    color: Colors.textPrimary,
  },
  rowWrap: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  sleepChip: {
    paddingVertical: 9,
    paddingHorizontal: 13,
    borderRadius: 12,
    backgroundColor: 'rgba(255,255,255,0.05)',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.1)',
  },
  powerBtn: {
    height: 50,
    borderRadius: 16,
    backgroundColor: Colors.dangerBg,
    borderWidth: 1,
    borderColor: 'rgba(248, 113, 113, 0.4)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  powerBtnText: {
    color: Colors.danger,
    fontSize: 14,
    fontWeight: '700',
  },
});
