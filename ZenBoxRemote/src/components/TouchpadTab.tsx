import React, { useMemo, useRef } from 'react';
import { PanResponder, StyleSheet, Text, View } from 'react-native';
import * as Haptics from 'expo-haptics';
import { Colors } from '../theme/colors';
import { zenSocket } from '../services/ZenBoxSocket';

export const TouchpadSurface: React.FC = () => {
  const lastMoveRef = useRef({ x: 0, y: 0, time: 0 });
  const touchStartRef = useRef({ time: 0, moved: false });
  const lastScrollYRef = useRef(0);

  const cursorPanResponder = useMemo(
    () =>
      PanResponder.create({
        onStartShouldSetPanResponder: () => true,
        onMoveShouldSetPanResponder: () => true,
        onPanResponderGrant: (_, gesture) => {
          lastMoveRef.current = { x: gesture.x0, y: gesture.y0, time: Date.now() };
          touchStartRef.current = { time: Date.now(), moved: false };
        },
        onPanResponderMove: (_, gesture) => {
          const now = Date.now();
          if (Math.abs(gesture.dx) > 6 || Math.abs(gesture.dy) > 6) {
            touchStartRef.current.moved = true;
          }
          if (now - lastMoveRef.current.time < 16) return;

          const stepX = (gesture.moveX - lastMoveRef.current.x) * 1.8;
          const stepY = (gesture.moveY - lastMoveRef.current.y) * 1.8;
          lastMoveRef.current = { x: gesture.moveX, y: gesture.moveY, time: now };

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

  return (
    <View style={styles.padRow}>
      <View style={styles.touchpadBox} {...cursorPanResponder.panHandlers}>
        <Text style={styles.hintMain} pointerEvents="none">
          Vuốt để di chuyển chuột • Chạm để bấm
        </Text>
      </View>

      <View style={styles.scrollBar} {...scrollPanResponder.panHandlers}>
        <Text style={styles.scrollIcon}>▲</Text>
        <Text style={styles.scrollText}>Cuộn</Text>
        <Text style={styles.scrollIcon}>▼</Text>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  padRow: {
    flex: 1,
    width: '100%',
    flexDirection: 'row',
    gap: 10,
  },
  touchpadBox: {
    flex: 1,
    borderRadius: 24,
    backgroundColor: 'rgba(255, 255, 255, 0.05)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 16,
  },
  hintMain: {
    color: Colors.textMuted,
    fontSize: 14,
    fontWeight: '600',
    textAlign: 'center',
  },
  scrollBar: {
    width: 52,
    borderRadius: 22,
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 18,
  },
  scrollIcon: {
    color: Colors.textSecondary,
    fontSize: 13,
  },
  scrollText: {
    color: Colors.textMuted,
    fontSize: 11,
    fontWeight: '700',
  },
});
