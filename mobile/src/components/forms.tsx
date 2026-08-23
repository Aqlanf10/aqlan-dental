import { colors, radius, shadow, spacing } from "@/theme";
import React from "react";
import {
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
  type KeyboardTypeOptions,
  type TextInputProps
} from "react-native";

export function FormField({
  label,
  value,
  onChangeText,
  placeholder,
  keyboardType,
  secureTextEntry,
  multiline = false,
  autoCapitalize,
  maxLength
}: {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  placeholder?: string;
  keyboardType?: KeyboardTypeOptions;
  secureTextEntry?: boolean;
  multiline?: boolean;
  autoCapitalize?: TextInputProps["autoCapitalize"];
  maxLength?: number;
}) {
  return (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        accessibilityLabel={label}
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        placeholderTextColor={colors.muted}
        keyboardType={keyboardType}
        secureTextEntry={secureTextEntry}
        multiline={multiline}
        autoCapitalize={autoCapitalize}
        maxLength={maxLength}
        textAlign="right"
        style={[styles.input, multiline && styles.multiline]}
      />
    </View>
  );
}

export function ChoiceRow({
  label,
  value,
  options,
  onChange
}: {
  label: string;
  value: string | null;
  options: Array<{ label: string; value: string }>;
  onChange: (value: string | null) => void;
}) {
  return (
    <View style={styles.field} accessibilityLabel={label}>
      <Text style={styles.label}>{label}</Text>
      <View style={styles.choices}>
        {options.map((option) => {
          const selected = value === option.value;
          return (
            <Pressable
              key={option.value}
              accessibilityRole="button"
              accessibilityLabel={`${label}: ${option.label}`}
              accessibilityState={{ selected }}
              onPress={() => onChange(selected ? null : option.value)}
              style={[styles.choice, selected && styles.choiceSelected]}
            >
              <Text style={[styles.choiceText, selected && styles.choiceTextSelected]}>{option.label}</Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

export function SelectList({
  label,
  value,
  options,
  onChange,
  emptyLabel = "بدون تحديد"
}: {
  label: string;
  value: string | null;
  options: Array<{ label: string; value: string; subtitle?: string | null }>;
  onChange: (value: string | null) => void;
  emptyLabel?: string;
}) {
  return (
    <View style={styles.field} accessibilityLabel={label}>
      <Text style={styles.label}>{label}</Text>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`${label}: ${emptyLabel}`}
        accessibilityState={{ selected: value === null }}
        onPress={() => onChange(null)}
        style={[styles.selectOption, value === null && styles.selectOptionSelected]}
      >
        <Text style={[styles.selectText, value === null && styles.selectTextSelected]}>{emptyLabel}</Text>
      </Pressable>
      {options.map((option) => {
        const selected = value === option.value;
        return (
          <Pressable
            key={option.value}
            accessibilityRole="button"
            accessibilityLabel={`${label}: ${option.label}${option.subtitle ? `، ${option.subtitle}` : ""}`}
            accessibilityState={{ selected }}
            onPress={() => onChange(option.value)}
            style={[styles.selectOption, selected && styles.selectOptionSelected]}
          >
            <View style={{ flex: 1 }}>
              <Text style={[styles.selectText, selected && styles.selectTextSelected]}>{option.label}</Text>
              {option.subtitle ? <Text style={styles.selectSubtitle}>{option.subtitle}</Text> : null}
            </View>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  field: { gap: spacing.xs },
  label: { color: colors.text, fontSize: 14, fontWeight: "700", textAlign: "right" },
  input: { minHeight: 52, borderWidth: 1, borderColor: colors.border, borderRadius: radius.sm, backgroundColor: colors.surface, color: colors.text, paddingHorizontal: spacing.md, paddingVertical: spacing.sm, textAlign: "right", ...shadow.card },
  multiline: { minHeight: 96, textAlignVertical: "top" },
  choices: { flexDirection: "row-reverse", flexWrap: "wrap", gap: spacing.sm },
  choice: { minHeight: 44, justifyContent: "center", borderWidth: 1, borderColor: colors.border, borderRadius: radius.pill, backgroundColor: colors.surface, paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  choiceSelected: { borderColor: colors.accent, backgroundColor: colors.accentSoft },
  choiceText: { color: colors.text, fontWeight: "600" },
  choiceTextSelected: { color: colors.accentDark },
  selectOption: { minHeight: 48, flexDirection: "row-reverse", alignItems: "center", borderWidth: 1, borderColor: colors.border, borderRadius: radius.sm, backgroundColor: colors.surface, paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  selectOptionSelected: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  selectText: { color: colors.text, textAlign: "right", fontWeight: "600" },
  selectTextSelected: { color: colors.primary },
  selectSubtitle: { color: colors.muted, fontSize: 12, textAlign: "right", marginTop: 2 }
});
