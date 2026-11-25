import React, { useState } from 'react';
import {
  Keyboard,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import * as Haptics from 'expo-haptics';
import { Colors } from '../theme/colors';
import { SearchTarget } from '../types/remote';
import { zenSocket } from '../services/ZenBoxSocket';

interface RealtimeSearchBarProps {
  connected: boolean;
  activeSection: string;
}

const TARGET_SCOPES: { key: SearchTarget; label: string }[] = [
  { key: 'CURRENT', label: 'Đang mở' },
  { key: 'YOUTUBE', label: 'YouTube' },
  { key: 'SPOTIFY', label: 'Spotify' },
  { key: 'NETFLIX', label: 'Netflix' },
  { key: 'TIKTOK', label: 'TikTok' },
  { key: 'GOOGLE', label: 'Google' },
  { key: 'OFFLINE', label: 'Kho Phim' },
];

export const RealtimeSearchBar: React.FC<RealtimeSearchBarProps> = ({
  connected,
  activeSection,
}) => {
  const [query, setQuery] = useState('');
  const [target, setTarget] = useState<SearchTarget>('CURRENT');
  const [focused, setFocused] = useState(false);

  const handleChangeText = (text: string) => {
    setQuery(text);
    zenSocket.sendRealtimeSearch(text, target);
  };

  const handleClear = () => {
    void Haptics.selectionAsync();
    setQuery('');
    zenSocket.sendRealtimeSearch('', target);
  };

  const handleSubmit = () => {
    void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);
    zenSocket.submitSearch(query, target);
    Keyboard.dismiss();
  };

  const handleSelectTarget = (nextTarget: SearchTarget) => {
    void Haptics.selectionAsync();
    setTarget(nextTarget);
    if (query.trim().length > 0) {
      zenSocket.sendRealtimeSearch(query, nextTarget);
    }
  };

  const placeholderText =
    target === 'CURRENT'
      ? activeSection === 'Home'
        ? 'Nhập chữ tìm ngay trên YouTube...'
        : 'Gõ chữ tìm kiếm trực tiếp trên TV...'
      : `Gõ tìm realtime trên ${TARGET_SCOPES.find((s) => s.key === target)?.label}...`;

  return (
    <View style={styles.wrapper}>
      <View
        style={[
          styles.searchBox,
          focused && styles.searchBoxFocused,
          !connected && styles.searchBoxDisabled,
        ]}
      >
        <Text style={styles.searchIcon}>⌕</Text>
        <TextInput
          value={query}
          onChangeText={handleChangeText}
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
          onSubmitEditing={handleSubmit}
          placeholder={placeholderText}
          placeholderTextColor={Colors.textMuted}
          returnKeyType="search"
          autoCorrect={false}
          autoCapitalize="none"
          style={styles.input}
        />

        {query.length > 0 && (
          <TouchableOpacity
            onPress={handleClear}
            style={styles.clearBtn}
            hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
          >
            <Text style={styles.clearText}>✕</Text>
          </TouchableOpacity>
        )}

        <TouchableOpacity
          onPress={handleSubmit}
          activeOpacity={0.8}
          style={[styles.submitBtn, query.trim().length > 0 && styles.submitBtnActive]}
        >
          <Text style={styles.submitText}>Tìm</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.scopeRowWrapper}>
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          keyboardShouldPersistTaps="handled"
          contentContainerStyle={styles.scopeScroll}
        >
          <View style={styles.liveBadge}>
            <View
              style={[
                styles.liveDot,
                { backgroundColor: connected ? Colors.accentCyan : Colors.textMuted },
              ]}
            />
            <Text style={styles.liveBadgeText}>GÕ TỨC THÌ</Text>
          </View>

          {TARGET_SCOPES.map((item) => {
            const isSelected = target === item.key;
            return (
              <TouchableOpacity
                key={item.key}
                onPress={() => handleSelectTarget(item.key)}
                activeOpacity={0.78}
                style={[styles.scopeChip, isSelected && styles.scopeChipActive]}
              >
                <Text
                  style={[
                    styles.scopeChipText,
                    isSelected && styles.scopeChipTextActive,
                  ]}
                >
                  {item.label}
                </Text>
              </TouchableOpacity>
            );
          })}
        </ScrollView>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  wrapper: {
    paddingHorizontal: 16,
    paddingTop: 6,
    paddingBottom: 8,
  },
  searchBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.glassFillStrong,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    paddingLeft: 14,
    paddingRight: 6,
    height: 50,
  },
  searchBoxFocused: {
    borderColor: Colors.glassBorderBright,
    backgroundColor: '#13203D',
  },
  searchBoxDisabled: {
    opacity: 0.78,
  },
  searchIcon: {
    fontSize: 20,
    color: Colors.accentCyan,
    marginRight: 8,
    marginTop: -2,
  },
  input: {
    flex: 1,
    color: Colors.textPrimary,
    fontSize: 15,
    paddingVertical: 8,
  },
  clearBtn: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: 'rgba(255,255,255,0.08)',
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 6,
  },
  clearText: {
    color: Colors.textSecondary,
    fontSize: 12,
    fontWeight: '700',
  },
  submitBtn: {
    height: 38,
    paddingHorizontal: 15,
    borderRadius: 13,
    backgroundColor: 'rgba(56, 189, 248, 0.16)',
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.32)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  submitBtnActive: {
    backgroundColor: Colors.accentBlue,
    borderColor: Colors.accentCyan,
  },
  submitText: {
    color: Colors.textPrimary,
    fontSize: 14,
    fontWeight: '700',
  },
  scopeRowWrapper: {
    marginTop: 8,
  },
  scopeScroll: {
    alignItems: 'center',
    paddingRight: 8,
    gap: 7,
  },
  liveBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 9,
    paddingVertical: 5,
    borderRadius: 10,
    backgroundColor: 'rgba(56, 189, 248, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.22)',
  },
  liveDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    marginRight: 5,
  },
  liveBadgeText: {
    color: Colors.accentCyan,
    fontSize: 10,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
  scopeChip: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 11,
    backgroundColor: 'rgba(255,255,255,0.05)',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
  },
  scopeChipActive: {
    backgroundColor: 'rgba(56, 189, 248, 0.22)',
    borderColor: Colors.accentCyan,
  },
  scopeChipText: {
    color: Colors.textSecondary,
    fontSize: 12,
    fontWeight: '600',
  },
  scopeChipTextActive: {
    color: Colors.textPrimary,
    fontWeight: '700',
  },
});
