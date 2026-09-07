import { Text, type TextProps } from 'react-native';

import { useLocale } from '@/i18n/LocaleProvider';
import { colors, typography } from '@/theme/tokens';

type Variant = keyof typeof typography;

type Props = TextProps & {
  variant?: Variant;
  color?: string;
};

export function AppText({ variant = 'body', color = colors.ink, style, ...props }: Props) {
  const { isRtl } = useLocale();
  return (
    <Text
      {...props}
      style={[
        typography[variant],
        { color, textAlign: isRtl ? 'right' : 'left', writingDirection: isRtl ? 'rtl' : 'ltr' },
        style,
      ]}
    />
  );
}
