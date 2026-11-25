import React from 'react';
import {
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import * as Haptics from 'expo-haptics';
import { LinearGradient } from 'expo-linear-gradient';
import { Colors } from '../theme/colors';
import { RemoteState } from '../types/remote';
import { zenSocket } from '../services/ZenBoxSocket';

interface RemoteDpadTabProps {
  state: RemoteState;
  connected: boolean;
}

const VOLUME_QUICK_STEPS = [20, 40, 60, 80, 100];

export const RemoteDpadTab: React.FC<RemoteDpadTabProps> = ({ state, connected }) => {
  const triggerAction = (
    action: string,
    heavy = false,
    extra?: { value?: number; target?: string }
  ) => {
    void Haptics.impactAsync(
      heavy ? Haptics.ImpactFeedbackStyle.Medium : Haptics.ImpactFeedbackStyle.Light
    );
    zenSocket.sendCommand({ action, ...extra });
  };

  return (
    <ScrollView
      contentContainerStyle={styles.container}
      showsVerticalScrollIndicator={false}
    >
      {/* Màn hình đang hiển thị trên TV */}
      <View style={styles.nowPlayingCard}>
        <View style={styles.nowPlayingLeft}>
          <Text style={styles.nowPlayingLabel}>ĐANG MỞ TRÊN TV</Text>
          <Text style={styles.nowPlayingTitle} numberOfLines={1}>
            {connected ? state.activeTitle : 'Chưa kết nối với TV'}
          </Text>
        </View>
        <View style={styles.nowPlayingPills}>
          <Text style={styles.presetBadge} numberOfLines={1}>
            ♪ {state.audioPresetLabel}
          </Text>
        </View>
      </View>

      {/* Hàng nút điều hướng nhanh: Trở về - Trang Chủ - Tải lại */}
      <View style={styles.navRow}>
        <TouchableOpacity
          style={styles.navButton}
          activeOpacity={0.75}
          onPress={() => triggerAction('BACK')}
        >
          <Text style={styles.navIcon}>←</Text>
          <Text style={styles.navText}>Trở về</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.navButton, styles.navButtonHome]}
          activeOpacity={0.8}
          onPress={() => triggerAction('HOME', true)}
        >
          <LinearGradient
            colors={['rgba(56,189,248,0.28)', 'rgba(37,99,235,0.34)']}
            start={{ x: 0, y: 0 }}
            end={{ x: 1, y: 1 }}
            style={styles.homeGradient}
          >
            <Text style={styles.navIconHome}>⌂</Text>
            <Text style={styles.navTextHome}>Trang Chủ</Text>
          </LinearGradient>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.navButton}
          activeOpacity={0.75}
          onPress={() => triggerAction('RELOAD')}
        >
          <Text style={styles.navIcon}>↻</Text>
          <Text style={styles.navText}>Tải lại</Text>
        </TouchableOpacity>
      </View>

      {/* Cụm điều hướng 5 chiều D-Pad tròn chuẩn điều khiển TV */}
      <View style={styles.dpadSection}>
        <View style={styles.dpadOuterRing}>
          {/* Lên */}
          <TouchableOpacity
            style={[styles.dpadArrow, styles.dpadUp]}
            activeOpacity={0.68}
            onPress={() => triggerAction('DPAD_UP')}
          >
            <Text style={styles.arrowGlyph}>▲</Text>
          </TouchableOpacity>

          {/* Hàng Giữa: Trái - OK - Phải */}
          <View style={styles.dpadMiddleRow}>
            <TouchableOpacity
              style={[styles.dpadArrow, styles.dpadSide]}
              activeOpacity={0.68}
              onPress={() => triggerAction('DPAD_LEFT')}
            >
              <Text style={styles.arrowGlyph}>◀</Text>
            </TouchableOpacity>

            <TouchableOpacity
              activeOpacity={0.82}
              onPress={() => triggerAction('DPAD_OK', true)}
              style={styles.okButtonOuter}
            >
              <LinearGradient
                colors={['#38BDF8', '#2563EB']}
                start={{ x: 0, y: 0 }}
                end={{ x: 1, y: 1 }}
                style={styles.okButtonInner}
              >
                <Text style={styles.okText}>OK</Text>
              </LinearGradient>
            </TouchableOpacity>

            <TouchableOpacity
              style={[styles.dpadArrow, styles.dpadSide]}
              activeOpacity={0.68}
              onPress={() => triggerAction('DPAD_RIGHT')}
            >
              <Text style={styles.arrowGlyph}>▶</Text>
            </TouchableOpacity>
          </View>

          {/* Xuống */}
          <TouchableOpacity
            style={[styles.dpadArrow, styles.dpadDown]}
            activeOpacity={0.68}
            onPress={() => triggerAction('DPAD_DOWN')}
          >
            <Text style={styles.arrowGlyph}>▼</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Cụm điều khiển phát Phim / Nhạc / Video */}
      <View style={styles.transportRow}>
        <TouchableOpacity
          style={styles.transportBtn}
          activeOpacity={0.75}
          onPress={() => triggerAction('SEEK_BACK')}
        >
          <Text style={styles.transportGlyph}>⟲ 15s</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.transportBtn, styles.transportPlayBtn]}
          activeOpacity={0.8}
          onPress={() => triggerAction('PLAY_PAUSE', true)}
        >
          <Text style={styles.transportPlayGlyph}>⏯ Phát / Tạm dừng</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.transportBtn}
          activeOpacity={0.75}
          onPress={() => triggerAction('SEEK_FORWARD')}
        >
          <Text style={styles.transportGlyph}>15s ⟳</Text>
        </TouchableOpacity>
      </View>

      {/* Cụm điều khiển Âm lượng tổng đồng bộ hai chiều với Windows 11 */}
      <View style={styles.volumeCard}>
        <View style={styles.volumeHeader}>
          <Text style={styles.volumeLabel}>Âm lượng TV / Máy tính</Text>
          <Text style={styles.volumeValueText}>
            {state.isMuted ? 'Đang tắt tiếng' : `${state.volume}%`}
          </Text>
        </View>

        <View style={styles.volumeControlsRow}>
          <TouchableOpacity
            style={[
              styles.volCircleBtn,
              state.isMuted && styles.volCircleBtnMuted,
            ]}
            activeOpacity={0.75}
            onPress={() => triggerAction('MUTE_TOGGLE', true)}
          >
            <Text style={styles.volBtnText}>{state.isMuted ? '🔇' : '🔊'}</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.volStepBtn}
            activeOpacity={0.75}
            onPress={() => triggerAction('VOL_DOWN')}
          >
            <Text style={styles.volStepSign}>−</Text>
          </TouchableOpacity>

          <View style={styles.volTrackOuter}>
            <LinearGradient
              colors={
                state.isMuted
                  ? ['#475569', '#64748B']
                  : ['#38BDF8', '#2563EB']
              }
              start={{ x: 0, y: 0 }}
              end={{ x: 1, y: 0 }}
              style={[
                styles.volTrackFill,
                { width: `${Math.max(4, Math.min(100, state.volume))}%` },
              ]}
            />
          </View>

          <TouchableOpacity
            style={styles.volStepBtn}
            activeOpacity={0.75}
            onPress={() => triggerAction('VOL_UP')}
          >
            <Text style={styles.volStepSign}>+</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.quickVolPresetsRow}>
          {VOLUME_QUICK_STEPS.map((pct) => (
            <TouchableOpacity
              key={pct}
              style={[
                styles.quickVolChip,
                Math.abs(state.volume - pct) <= 4 && !state.isMuted && styles.quickVolChipActive,
              ]}
              activeOpacity={0.75}
              onPress={() => triggerAction('SET_VOLUME', false, { value: pct })}
            >
              <Text style={styles.quickVolChipText}>{pct}%</Text>
            </TouchableOpacity>
          ))}
        </View>
      </View>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: 16,
    paddingBottom: 24,
    gap: 14,
  },
  nowPlayingCard: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: Colors.glassFill,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    paddingHorizontal: 14,
    paddingVertical: 11,
  },
  nowPlayingLeft: {
    flex: 1,
    marginRight: 10,
  },
  nowPlayingLabel: {
    fontSize: 10,
    fontWeight: '700',
    color: Colors.accentCyan,
    letterSpacing: 0.7,
    marginBottom: 2,
  },
  nowPlayingTitle: {
    fontSize: 15,
    fontWeight: '700',
    color: Colors.textPrimary,
  },
  nowPlayingPills: {
    backgroundColor: 'rgba(56, 189, 248, 0.12)',
    borderRadius: 10,
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.25)',
  },
  presetBadge: {
    color: Colors.accentCyan,
    fontSize: 11,
    fontWeight: '700',
  },
  navRow: {
    flexDirection: 'row',
    gap: 10,
  },
  navButton: {
    flex: 1,
    height: 52,
    borderRadius: 16,
    backgroundColor: Colors.glassFill,
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
  },
  navButtonHome: {
    flex: 1.35,
    padding: 0,
    overflow: 'hidden',
    borderColor: 'rgba(56, 189, 248, 0.45)',
  },
  homeGradient: {
    flex: 1,
    height: '100%',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 7,
  },
  navIcon: {
    fontSize: 18,
    color: Colors.textPrimary,
    fontWeight: '700',
  },
  navText: {
    fontSize: 14,
    color: Colors.textPrimary,
    fontWeight: '600',
  },
  navIconHome: {
    fontSize: 19,
    color: Colors.accentCyan,
    fontWeight: '800',
  },
  navTextHome: {
    fontSize: 15,
    color: Colors.textPrimary,
    fontWeight: '700',
  },
  dpadSection: {
    alignItems: 'center',
    justifyContent: 'center',
    marginVertical: 4,
  },
  dpadOuterRing: {
    width: 228,
    height: 228,
    borderRadius: 114,
    backgroundColor: 'rgba(15, 24, 46, 0.92)',
    borderWidth: 1.5,
    borderColor: 'rgba(148, 163, 184, 0.22)',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 10,
    shadowColor: '#000',
    shadowOpacity: 0.45,
    shadowRadius: 18,
    shadowOffset: { width: 0, height: 8 },
    elevation: 10,
  },
  dpadMiddleRow: {
    width: '100%',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  dpadArrow: {
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 20,
  },
  dpadUp: {
    width: 96,
    height: 54,
  },
  dpadDown: {
    width: 96,
    height: 54,
  },
  dpadSide: {
    width: 54,
    height: 96,
  },
  arrowGlyph: {
    color: Colors.textSecondary,
    fontSize: 18,
  },
  okButtonOuter: {
    width: 88,
    height: 88,
    borderRadius: 44,
    padding: 3,
    backgroundColor: 'rgba(56, 189, 248, 0.25)',
  },
  okButtonInner: {
    flex: 1,
    borderRadius: 42,
    alignItems: 'center',
    justifyContent: 'center',
  },
  okText: {
    color: '#FFFFFF',
    fontSize: 20,
    fontWeight: '800',
    letterSpacing: 1,
  },
  transportRow: {
    flexDirection: 'row',
    gap: 10,
  },
  transportBtn: {
    flex: 1,
    height: 48,
    borderRadius: 15,
    backgroundColor: Colors.glassFill,
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    alignItems: 'center',
    justifyContent: 'center',
  },
  transportPlayBtn: {
    flex: 1.6,
    backgroundColor: 'rgba(56, 189, 248, 0.14)',
    borderColor: 'rgba(56, 189, 248, 0.36)',
  },
  transportGlyph: {
    color: Colors.textPrimary,
    fontSize: 13,
    fontWeight: '700',
  },
  transportPlayGlyph: {
    color: Colors.textPrimary,
    fontSize: 14,
    fontWeight: '700',
  },
  volumeCard: {
    backgroundColor: Colors.glassFill,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    padding: 14,
    gap: 11,
  },
  volumeHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  volumeLabel: {
    color: Colors.textSecondary,
    fontSize: 13,
    fontWeight: '600',
  },
  volumeValueText: {
    color: Colors.accentCyan,
    fontSize: 14,
    fontWeight: '800',
  },
  volumeControlsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  volCircleBtn: {
    width: 44,
    height: 44,
    borderRadius: 14,
    backgroundColor: 'rgba(255,255,255,0.06)',
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    alignItems: 'center',
    justifyContent: 'center',
  },
  volCircleBtnMuted: {
    backgroundColor: Colors.dangerBg,
    borderColor: Colors.danger,
  },
  volBtnText: {
    fontSize: 18,
  },
  volStepBtn: {
    width: 44,
    height: 44,
    borderRadius: 14,
    backgroundColor: 'rgba(255,255,255,0.08)',
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    alignItems: 'center',
    justifyContent: 'center',
  },
  volStepSign: {
    color: Colors.textPrimary,
    fontSize: 22,
    fontWeight: '700',
    marginTop: -2,
  },
  volTrackOuter: {
    flex: 1,
    height: 12,
    borderRadius: 6,
    backgroundColor: 'rgba(255,255,255,0.08)',
    overflow: 'hidden',
  },
  volTrackFill: {
    height: '100%',
    borderRadius: 6,
  },
  quickVolPresetsRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: 8,
  },
  quickVolChip: {
    flex: 1,
    paddingVertical: 6,
    borderRadius: 10,
    backgroundColor: 'rgba(255,255,255,0.04)',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
    alignItems: 'center',
  },
  quickVolChipActive: {
    backgroundColor: 'rgba(56, 189, 248, 0.2)',
    borderColor: Colors.accentCyan,
  },
  quickVolChipText: {
    color: Colors.textSecondary,
    fontSize: 12,
    fontWeight: '700',
  },
});
