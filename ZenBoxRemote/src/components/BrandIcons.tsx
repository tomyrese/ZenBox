import React from 'react';
import Svg, {
  Circle,
  Defs,
  LinearGradient,
  Path,
  Polygon,
  Rect,
  Stop,
} from 'react-native-svg';

export type AppBrandKey =
  | 'YOUTUBE'
  | 'NETFLIX'
  | 'SPOTIFY'
  | 'TIKTOK'
  | 'FACEBOOK'
  | 'FPTPLAY'
  | 'VTVGO'
  | 'ZINGMP3'
  | 'APPLEMUSIC'
  | 'PRIMEVIDEO'
  | 'INSTAGRAM'
  | 'SOUNDCLOUD'
  | 'TWITCH'
  | 'WEB'
  | 'MOVIES'
  | 'MUSIC'
  | 'CONFIG';

export const BrandLogo: React.FC<{ brand: AppBrandKey; size?: number }> = ({
  brand,
  size = 40,
}) => {
  switch (brand) {
    case 'YOUTUBE':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Rect x="4" y="10" width="40" height="28" rx="9" fill="#FF0033" />
          <Polygon points="20,17 20,31 32,24" fill="#FFFFFF" />
        </Svg>
      );

    case 'NETFLIX':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Path d="M12 7H19V41H12V7Z" fill="#B1060F" />
          <Path d="M29 7H36V41H29V7Z" fill="#B1060F" />
          <Polygon points="12,7 19,7 36,41 29,41" fill="#E50914" />
        </Svg>
      );

    case 'SPOTIFY':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Circle cx="24" cy="24" r="19" fill="#1ED760" />
          <Path
            d="M14 19.5C21 17.3 29 17.8 34.8 21.2"
            stroke="#050811"
            strokeWidth="3.4"
            strokeLinecap="round"
            fill="none"
          />
          <Path
            d="M15.8 25.2C21.5 23.5 28 24 32.8 26.8"
            stroke="#050811"
            strokeWidth="2.9"
            strokeLinecap="round"
            fill="none"
          />
          <Path
            d="M17.5 30.5C22 29.2 27 29.6 30.8 31.8"
            stroke="#050811"
            strokeWidth="2.5"
            strokeLinecap="round"
            fill="none"
          />
        </Svg>
      );

    case 'TIKTOK':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Path
            d="M27.5 9V29.5C27.5 33.1 24.6 36 21 36C17.4 36 14.5 33.1 14.5 29.5C14.5 25.9 17.4 23 21 23V18.2C14.8 18.2 9.8 23.2 9.8 29.5C9.8 35.8 14.8 40.8 21 40.8C27.2 40.8 32.2 35.8 32.2 29.5V19.2C34.5 21 37.3 22 40.2 22V17.2C35.8 17.2 32.2 13.6 32.2 9H27.5Z"
            fill="#25F4EE"
            transform="translate(-1.5, -1)"
          />
          <Path
            d="M27.5 9V29.5C27.5 33.1 24.6 36 21 36C17.4 36 14.5 33.1 14.5 29.5C14.5 25.9 17.4 23 21 23V18.2C14.8 18.2 9.8 23.2 9.8 29.5C9.8 35.8 14.8 40.8 21 40.8C27.2 40.8 32.2 35.8 32.2 29.5V19.2C34.5 21 37.3 22 40.2 22V17.2C35.8 17.2 32.2 13.6 32.2 9H27.5Z"
            fill="#FE2C55"
            transform="translate(1.5, 1)"
          />
          <Path
            d="M27.5 9V29.5C27.5 33.1 24.6 36 21 36C17.4 36 14.5 33.1 14.5 29.5C14.5 25.9 17.4 23 21 23V18.2C14.8 18.2 9.8 23.2 9.8 29.5C9.8 35.8 14.8 40.8 21 40.8C27.2 40.8 32.2 35.8 32.2 29.5V19.2C34.5 21 37.3 22 40.2 22V17.2C35.8 17.2 32.2 13.6 32.2 9H27.5Z"
            fill="#FFFFFF"
          />
        </Svg>
      );

    case 'FACEBOOK':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Circle cx="24" cy="24" r="19" fill="#1877F2" />
          <Path
            d="M26.8 42.8V26.5H32.2L33 20.2H26.8V16.2C26.8 14.4 27.3 13.1 29.9 13.1H33.2V7.5C31.6 7.3 29.9 7.2 28.3 7.2C23.4 7.2 20.1 10.2 20.1 15.6V20.2H14.6V26.5H20.1V42.8H26.8Z"
            fill="#FFFFFF"
          />
        </Svg>
      );

    case 'FPTPLAY':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Defs>
            <LinearGradient id="fptGrad" x1="0" y1="0" x2="1" y2="1">
              <Stop offset="0" stopColor="#FF7A00" />
              <Stop offset="1" stopColor="#FF3D00" />
            </LinearGradient>
          </Defs>
          <Path d="M11 8L39 24L11 40V8Z" fill="url(#fptGrad)" />
          <Path d="M11 8L25 16L11 24V8Z" fill="#0066FF" />
          <Path d="M11 24L25 32L11 40V24Z" fill="#00C853" />
        </Svg>
      );

    case 'VTVGO':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Rect x="5" y="11" width="38" height="26" rx="8" fill="#E51C23" />
          <Polygon points="12,17 17,31 22,17 19,17 17,24 15,17" fill="#FFFFFF" />
          <Path d="M23 17H31V20H28.5V31H25.5V20H23V17Z" fill="#00A651" />
          <Polygon points="31,17 35,31 39,17 36.2,17 35,23.5 33.8,17" fill="#2979FF" />
        </Svg>
      );

    case 'ZINGMP3':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Defs>
            <LinearGradient id="zingGrad" x1="0" y1="0" x2="1" y2="1">
              <Stop offset="0" stopColor="#B721FF" />
              <Stop offset="1" stopColor="#21D4FD" />
            </LinearGradient>
          </Defs>
          <Circle cx="24" cy="24" r="19" fill="url(#zingGrad)" />
          <Path
            d="M17 16H31L18 32H32"
            stroke="#FFFFFF"
            strokeWidth="4"
            strokeLinecap="round"
            strokeLinejoin="round"
            fill="none"
          />
        </Svg>
      );

    case 'APPLEMUSIC':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Defs>
            <LinearGradient id="amGrad" x1="0" y1="0" x2="0" y2="1">
              <Stop offset="0" stopColor="#FA57C1" />
              <Stop offset="1" stopColor="#FA233B" />
            </LinearGradient>
          </Defs>
          <Path
            d="M34 9L18 12.5V31.2C16.9 30.5 15.5 30.2 14 30.5C11.2 31.1 9.5 33.6 10 36C10.5 38.4 13.2 39.8 16 39.2C18.5 38.7 20.5 36.6 20.5 34.1V19.2L31.5 16.8V28.2C30.4 27.5 29 27.2 27.5 27.5C24.7 28.1 23 30.6 23.5 33C24 35.4 26.7 36.8 29.5 36.2C32 35.7 34 33.6 34 31.1V9Z"
            fill="url(#amGrad)"
          />
        </Svg>
      );

    case 'PRIMEVIDEO':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Circle cx="24" cy="24" r="19" fill="#00A8E1" />
          <Polygon points="20,15 33,23 20,31" fill="#FFFFFF" />
          <Path
            d="M13 33.5C19 37.8 29 37.8 35.5 33"
            stroke="#FFFFFF"
            strokeWidth="2.8"
            strokeLinecap="round"
            fill="none"
          />
        </Svg>
      );

    case 'INSTAGRAM':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Defs>
            <LinearGradient id="igGrad" x1="0" y1="1" x2="1" y2="0">
              <Stop offset="0" stopColor="#FDCB52" />
              <Stop offset="0.5" stopColor="#FD1D1D" />
              <Stop offset="1" stopColor="#833AB4" />
            </LinearGradient>
          </Defs>
          <Rect
            x="7"
            y="7"
            width="34"
            height="34"
            rx="10"
            stroke="url(#igGrad)"
            strokeWidth="4"
            fill="none"
          />
          <Circle cx="24" cy="24" r="8" stroke="url(#igGrad)" strokeWidth="4" fill="none" />
          <Circle cx="34" cy="14" r="2.5" fill="#FD1D1D" />
        </Svg>
      );

    case 'SOUNDCLOUD':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Path
            d="M21 15C26.2 15 30.5 18.8 31.3 23.8C31.9 23.6 32.5 23.5 33.2 23.5C36.9 23.5 40 26.5 40 30.2C40 34 36.9 37 33.2 37H21V15Z"
            fill="#FF5500"
          />
          <Rect x="8" y="25" width="3" height="12" rx="1.5" fill="#FF7700" />
          <Rect x="12.5" y="21" width="3" height="16" rx="1.5" fill="#FF7700" />
          <Rect x="17" y="18" width="3" height="19" rx="1.5" fill="#FF5500" />
        </Svg>
      );

    case 'TWITCH':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Path d="M11 7L8 14V37H16V42H21L26 37H33L40 30V7H11Z" fill="#9146FF" />
          <Path d="M15 11H36V28L31 33H24L19 38V33H15V11Z" fill="#FFFFFF" />
          <Rect x="22" y="17" width="3.5" height="9" fill="#9146FF" />
          <Rect x="29" y="17" width="3.5" height="9" fill="#9146FF" />
        </Svg>
      );

    case 'WEB':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Path
            d="M41.6 24.4C41.6 23.1 41.5 21.9 41.2 20.7H24V27.8H33.9C33.5 30.1 32.1 32.1 30.1 33.4V38H36C39.5 34.8 41.6 30 41.6 24.4Z"
            fill="#4285F4"
          />
          <Path
            d="M24 42.4C29 42.4 33.1 40.7 36.1 38L30.2 33.4C28.5 34.5 26.5 35.2 24 35.2C19.2 35.2 15.2 32 13.7 27.6H7.6V32.3C10.6 38.3 16.8 42.4 24 42.4Z"
            fill="#34A853"
          />
          <Path
            d="M13.7 27.6C13.3 26.5 13.1 25.3 13.1 24C13.1 22.7 13.3 21.5 13.7 20.4V15.7H7.6C6.3 18.2 5.6 21 5.6 24C5.6 27 6.3 29.8 7.6 32.3L13.7 27.6Z"
            fill="#FBBC05"
          />
          <Path
            d="M24 12.8C26.7 12.8 29.1 13.7 31 15.6L36.2 10.4C33.1 7.5 29 5.6 24 5.6C16.8 5.6 10.6 9.7 7.6 15.7L13.7 20.4C15.2 16 19.2 12.8 24 12.8Z"
            fill="#EA4335"
          />
        </Svg>
      );

    case 'MOVIES':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Rect x="6" y="10" width="36" height="28" rx="6" fill="#F59E0B" />
          <Path d="M6 18H42" stroke="#78350F" strokeWidth="2.5" />
          <Polygon points="21,23 30,28 21,33" fill="#FFFFFF" />
        </Svg>
      );

    case 'MUSIC':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Defs>
            <LinearGradient id="musGrad" x1="0" y1="0" x2="1" y2="1">
              <Stop offset="0" stopColor="#EC4899" />
              <Stop offset="1" stopColor="#8B5CF6" />
            </LinearGradient>
          </Defs>
          <Path
            d="M12 28V22C12 15.4 17.4 10 24 10C30.6 10 36 15.4 36 22V28"
            stroke="url(#musGrad)"
            strokeWidth="4.5"
            strokeLinecap="round"
            fill="none"
          />
          <Rect x="8" y="25" width="7" height="13" rx="3.5" fill="#EC4899" />
          <Rect x="33" y="25" width="7" height="13" rx="3.5" fill="#8B5CF6" />
        </Svg>
      );

    case 'CONFIG':
      return (
        <Svg width={size} height={size} viewBox="0 0 48 48">
          <Path d="M10 15H38M10 24H38M10 33H38" stroke="#38BDF8" strokeWidth="3.5" strokeLinecap="round" />
          <Circle cx="18" cy="15" r="4" fill="#F8FAFC" />
          <Circle cx="30" cy="24" r="4" fill="#38BDF8" />
          <Circle cx="21" cy="33" r="4" fill="#F8FAFC" />
        </Svg>
      );
  }
};
