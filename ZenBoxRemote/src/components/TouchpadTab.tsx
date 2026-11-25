import React, { useMemo, useRef } from 'react';
import {
  PanResponder,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import * as Haptics from 'expo-haptics';
import { Colors } from '../theme/colors';
import { zenSocket } from '../services/ZenBoxSocket';

export const TouchpadTab: React.FC = () => {
  const lastMoveRef = useRef({ x: 0, y: 0, time: 0 });
  const touchStartRef = useRef({ time: 0, moved: false });
  const lastScrollYRef = useRef(0);

  const cursorPanResponder = useMemo(
    () =>
      PanResponder.create({
        onStartShouldSetPanResponder: () => true,
        onMoveShouldSetPanResponder: () => true,
        onPanResponderGrant: (_, gesture) => {
          lastMoveRef.current = {
            x: gesture.x0,
            y: gesture.y0,
            time: Date.now(),
          };
          touchStartRef.current = {
            time: Date.now(),
            moved: false,
          };
        },
        onPanResponderMove: (_, gesture) => {
          const now = Date.now();
          if (Math.abs(gesture.dx) > 6 || Math.abs(gesture.dy) > 6) {
            touchStartRef.current.moved = true;
          }

          if (now - lastMoveRef.current.time < 16) {
            return;
          }

          const stepX = (gesture.moveX - lastMoveRef.current.x) * 1.75;
          const stepY = (gesture.moveY - lastMoveRef.current.y) * 1.75;

          lastMoveRef.current = {
            x: gesture.moveX,
            y: gesture.moveY,
            time: now,
          };

          if (Math.abs(stepX) >= 0.5 || Math.abs(stepY) >= 0.5) {
            zenSocket.sendCommand({
              action: 'TOUCHPAD_MOVE',
              dx: Math.round(stepX * 10) / 10,
              dy: Math.round(stepY * 10) / 10,
            });
          }
        },
        onPanResponderRelease: () => {
          const elapsed = Date.now() - touchStartRef.current.time;
          if (!touchStartRef.current.moved && elapsed < 240) {
            void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
            zenSocket.sendCommand({ action: 'TOUCHPAD_CLICK' });
          }
        },
      }),
    []
  );

  const scrollPanResponder = useMemo(
    () =>
      PanResponder.create({
        onStartShouldSetPanResponder: () => true,
        onMoveShouldSetPanResponder: () => true,
        onPanResponderGrant: (_, gesture) => {
          lastScrollYRef.current = gesture.y0;
          void Haptics.selectionAsync();
        },
        onPanResponderMove: (_, gesture) => {
          const deltaY = lastScrollYRef.current - gesture.moveY;
          if (Math.abs(deltaY) >= 8) {
            lastScrollYRef.current = gesture.moveY;
            zenSocket.sendCommand({
              action: 'TOUCHPAD_SCROLL',
              dy: -deltaY * 1.4,
            });
          }
        },
      }),
    []
  );

  const quickAction = (action: string) => {
    void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
    zenSocket.sendCommand({ action });
  };

  return (
    <View style={styles.container}>
      <View style={styles.padRow}>
        {/* Bề mặt bàn rê chuột ảo AirMouse */}
        <View style={styles.touchpadSurface} {...cursorPanResponder.panHandlers}>
          <View style={styles.centerCrosshair} pointerEvents="none">
            <Text style={styles.padTitle}>Bàn Rê Chuột Ảo</Text>
            <Text style={styles.padSubtitle}>
              Vuốt 1 ngón để di chuyển con trỏ trên TV{'\n'}Chạm nhẹ 1 lần để bấm chọn
            </Text>
          </View>
        </View>

        {/* Thanh cuộn dọc chuyên dụng cho TikTok / Facebook / YouTube */}
        <View style={styles.scrollStrip} {...scrollPanResponder.panHandlers}>
          <Text style={styles.scrollArrow}>▲</Text>
          <Text style={styles.scrollLabel}>CUỘN{'\n'}TRANG</Text>
          <Text style={styles.scrollArrow}>▼</Text>
        </View>
      </View>

      {/* Hàng phím chuột & điều hướng dưới chân bàn rê */}
      <View style={styles.bottomActions}>
        <TouchableOpacity
          style={styles.actionBtn}
          activeOpacity={0.75}
          onPress={() => quickAction('BACK')}
        >
          <Text style={styles.actionText}>← Trở về</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.actionBtn, styles.leftClickBtn]}
          activeOpacity={0.8}
          onPress={() => quickAction('TOUCHPAD_CLICK')}
        >
          <Text style={styles.leftClickText}>● Bấm Chuột Trái</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.actionBtn}
          activeOpacity={0.75}
          onPress={() => quickAction('HOME')}
        >
          <Text style={styles.actionText}>⌂ Trang Chủ</Text>
        </TouchableOpacity>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    paddingHorizontal: 16,
    paddingBottom: 16,
    gap: 12,
  },
  padRow: {
    flex: 1,
    flexDirection: 'row',
    gap: 10,
    minHeight: 300,
  },
  touchpadSurface: {
    flex: 1,
    borderRadius: 24,
    backgroundColor: 'rgba(15, 24, 46, 0.88)',
    borderWidth: 1.5,
    borderColor: 'rgba(56, 189, 248, 0.28)',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 20,
  },
  centerCrosshair: {
    alignItems: 'center',
    opacity: 0.72,
  },
  padTitle: {
    color: Colors.accentCyan,
    fontSize: 16,
    fontWeight: '700',
    marginBottom: 6,
  },
  padSubtitle: {
    color: Colors.textSecondary,
    fontSize: 13,
    textAlign: 'center',
    lineHeight: 20,
  },
  scrollStrip: {
    width: 58,
    borderRadius: 22,
    backgroundColor: Colors.glassFillStrong,
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 22,
  },
  scrollArrow: {
    color: Colors.accentCyan,
    fontSize: 15,
  },
  scrollLabel: {
    color: Colors.textSecondary,
    fontSize: 10,
    fontWeight: '800',
    textAlign: 'center',
    letterSpacing: 0.6,
    lineHeight: 15,
  },
  bottomActions: {
    flexDirection: 'row',
    gap: 10,
  },
  actionBtn: {
    flex: 1,
    height: 50,
    borderRadius: 16,
    backgroundColor: Colors.glassFill,
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    alignItems: 'center',
    justifyContent: 'center',
  },
  leftClickBtn: {
    flex: 1.45,
    backgroundColor: 'rgba(56, 189, 248, 0.18)',
    borderColor: Colors.accentCyan,
  },
  actionText: {
    color: Colors.textPrimary,
    fontSize: 13,
    fontWeight: '600',
  },
  leftClickText: {
    color: Colors.textPrimary,
    fontSize: 14,
    fontWeight: '700',
  },
});
