import React from 'react';
import {
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import * as Haptics from 'expo-haptics';
import { AppBrandKey, BrandLogo } from './BrandIcons';
import { Colors } from '../theme/colors';
import { zenSocket } from '../services/ZenBoxSocket';

interface AppsGridTabProps {
  activeSection: string;
}

interface AppItem {
  target: string;
  sectionName: string;
  brand: AppBrandKey;
  label: string;
}

const ONLINE_APPS: AppItem[] = [
  { target: 'YOUTUBE', sectionName: 'YouTube', brand: 'YOUTUBE', label: 'YouTube' },
  { target: 'NETFLIX', sectionName: 'Netflix', brand: 'NETFLIX', label: 'Netflix' },
  { target: 'SPOTIFY', sectionName: 'Spotify', brand: 'SPOTIFY', label: 'Spotify' },
  { target: 'TIKTOK', sectionName: 'TikTok', brand: 'TIKTOK', label: 'TikTok' },
  { target: 'FACEBOOK', sectionName: 'Facebook', brand: 'FACEBOOK', label: 'Facebook' },
  { target: 'FPTPLAY', sectionName: 'FptPlay', brand: 'FPTPLAY', label: 'FPT Play' },
  { target: 'VTVGO', sectionName: 'VtvGo', brand: 'VTVGO', label: 'VTV Go' },
  { target: 'ZINGMP3', sectionName: 'ZingMp3', brand: 'ZINGMP3', label: 'Zing MP3' },
  { target: 'APPLEMUSIC', sectionName: 'AppleMusic', brand: 'APPLEMUSIC', label: 'Apple Music' },
  { target: 'PRIMEVIDEO', sectionName: 'PrimeVideo', brand: 'PRIMEVIDEO', label: 'Prime Video' },
  { target: 'INSTAGRAM', sectionName: 'Instagram', brand: 'INSTAGRAM', label: 'Instagram' },
  { target: 'SOUNDCLOUD', sectionName: 'SoundCloud', brand: 'SOUNDCLOUD', label: 'SoundCloud' },
  { target: 'TWITCH', sectionName: 'Twitch', brand: 'TWITCH', label: 'Twitch' },
  { target: 'WEB', sectionName: 'WebBrowser', brand: 'WEB', label: 'Google' },
];

const LOCAL_APPS: AppItem[] = [
  { target: 'MOVIES', sectionName: 'OfflineMovies', brand: 'MOVIES', label: 'Phim của tôi' },
  { target: 'MUSIC', sectionName: 'OfflineMusic', brand: 'MUSIC', label: 'Nhạc của tôi' },
  { target: 'CONFIG', sectionName: 'SystemConfig', brand: 'CONFIG', label: 'Cài đặt TV' },
];

export const AppsGridTab: React.FC<AppsGridTabProps> = ({ activeSection }) => {
  const openApp = (target: string) => {
    void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);
    zenSocket.sendCommand({
      action: 'OPEN_APP',
      target,
    });
  };

  return (
    <ScrollView
      contentContainerStyle={styles.container}
      showsVerticalScrollIndicator={false}
    >
      <Text style={styles.sectionTitle}>Giải trí trực tuyến (1 chạm mở ngay trên TV)</Text>
      <View style={styles.grid}>
        {ONLINE_APPS.map((item) => {
          const isCurrent = activeSection === item.sectionName;
          return (
            <TouchableOpacity
              key={item.target}
              style={[styles.tile, isCurrent && styles.tileActive]}
              activeOpacity={0.78}
              onPress={() => openApp(item.target)}
            >
              <BrandLogo brand={item.brand} size={38} />
              <Text style={styles.tileLabel} numberOfLines={1}>
                {item.label}
              </Text>
            </TouchableOpacity>
          );
        })}
      </View>

      <Text style={[styles.sectionTitle, { marginTop: 10 }]}>
        Thư viện Offline & Hệ thống
      </Text>
      <View style={styles.grid}>
        {LOCAL_APPS.map((item) => {
          const isCurrent = activeSection === item.sectionName;
          return (
            <TouchableOpacity
              key={item.target}
              style={[styles.tileWide, isCurrent && styles.tileActive]}
              activeOpacity={0.78}
              onPress={() => openApp(item.target)}
            >
              <BrandLogo brand={item.brand} size={34} />
              <Text style={styles.tileLabel} numberOfLines={1}>
                {item.label}
              </Text>
            </TouchableOpacity>
          );
        })}
      </View>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: 16,
    paddingBottom: 26,
    gap: 12,
  },
  sectionTitle: {
    color: Colors.textSecondary,
    fontSize: 13,
    fontWeight: '700',
    marginLeft: 2,
  },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
  },
  tile: {
    width: '31%',
    aspectRatio: 1.08,
    borderRadius: 18,
    backgroundColor: Colors.glassFill,
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 8,
    gap: 7,
  },
  tileWide: {
    width: '31%',
    height: 92,
    borderRadius: 18,
    backgroundColor: Colors.glassFill,
    borderWidth: 1,
    borderColor: Colors.glassBorder,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 8,
    gap: 6,
  },
  tileActive: {
    borderColor: Colors.accentCyan,
    backgroundColor: 'rgba(56, 189, 248, 0.16)',
  },
  tileLabel: {
    color: Colors.textPrimary,
    fontSize: 12,
    fontWeight: '600',
  },
});
