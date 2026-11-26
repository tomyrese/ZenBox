import React, { useState } from 'react';
import {
  Keyboard,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import * as Haptics from 'expo-haptics';
import { Colors } from '../theme/colors';
import { zenSocket } from '../services/ZenBoxSocket';

export const RealtimeSearchBar: React.FC = () => {
  const [query, setQuery] = useState('');
  const [focused, setFocused] = useState(false);

  const handleChangeText = (text: string) => {
    setQuery(text);
    zenSocket.sendRealtimeSearch(text, 'CURRENT');
  };

  const handleClear = () => {
    void Haptics.selectionAsync();
    setQuery('');
    zenSocket.sendRealtimeSearch('', 'CURRENT');
  };

  const handleSubmit = () => {
    void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);
    zenSocket.submitSearch(query, 'CURRENT');
    Keyboard.dismiss();
  };

  return (
    <View style={[styles.searchBox, focused && styles.searchBoxFocused]}>
      <Text style={styles.searchIcon}>⌕</Text>
      <TextInput
        value={query}
        onChangeText={handleChangeText}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onSubmitEditing={handleSubmit}
        placeholder="Nhập chữ để tìm trên TV..."
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

      { (focused || query.trim().length > 0) && (
        <TouchableOpacity
          onPress={handleSubmit}
          activeOpacity={0.8}
          style={styles.submitBtn}
        >
          <Text style={styles.submitText}>Tìm</Text>
        </TouchableOpacity>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  searchBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.07)',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
    paddingLeft: 14,
    paddingRight: 6,
    height: 48,
  },
  searchBoxFocused: {
    borderColor: Colors.accentCyan,
    backgroundColor: '#111D38',
  },
  searchIcon: {
    fontSize: 20,
    color: Colors.textSecondary,
    marginRight: 8,
    marginTop: -2,
  },
  input: {
    flex: 1,
    color: Colors.textPrimary,
    fontSize: 16,
    paddingVertical: 8,
  },
  clearBtn: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: 'rgba(255,255,255,0.1)',
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
    height: 36,
    paddingHorizontal: 16,
    borderRadius: 12,
    backgroundColor: Colors.accentBlue,
    alignItems: 'center',
    justifyContent: 'center',
  },
  submitText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '700',
  },
});
