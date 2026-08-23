import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import type {
  ConversationListItem,
  ConversationListResponse,
  MessagingUnreadCount
} from "@/lib/types";
import { colors, radius, shadow, spacing } from "@/theme";
import { router } from "expo-router";
import React, { useCallback, useEffect, useRef, useState } from "react";
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  TextInput,
  View
} from "react-native";

export default function MessagesScreen() {
  const [search, setSearch] = useState("");
  const [conversations, setConversations] = useState<ConversationListItem[]>([]);
  const [unread, setUnread] = useState<MessagingUnreadCount>({
    totalUnread: 0,
    unreadConversations: 0
  });
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const activeRequest = useRef<AbortController | null>(null);

  const load = useCallback(async (queryText: string) => {
    activeRequest.current?.abort();
    const controller = new AbortController();
    activeRequest.current = controller;
    setError(null);
    try {
      const params = new URLSearchParams({ page: "1", pageSize: "50" });
      if (queryText.trim()) params.set("search", queryText.trim());

      const [list, unreadCount] = await Promise.all([
        apiRequest<ConversationListResponse>(`/api/messages/conversations?${params.toString()}`, { signal: controller.signal }),
        apiRequest<MessagingUnreadCount>("/api/messages/unread-count", { signal: controller.signal })
      ]);

      if (controller.signal.aborted) return;
      setConversations(list.data ?? []);
      setUnread(unreadCount);
    } catch (err) {
      if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "تعذر تحميل المحادثات");
    } finally {
      if (activeRequest.current === controller) {
        activeRequest.current = null;
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => () => activeRequest.current?.abort(), []);

  useEffect(() => {
    const handle = setTimeout(() => {
      void load(search);
    }, 300);
    return () => {
      clearTimeout(handle);
      activeRequest.current?.abort();
    };
  }, [load, search]);

  async function refresh() {
    setRefreshing(true);
    try {
      await load(search);
    } finally {
      setRefreshing(false);
    }
  }

  return (
    <Screen scroll={false}>
      <View style={styles.header}>
        <View style={styles.unreadCard}>
          <Text style={styles.unread}>{unread.totalUnread} غير مقروءة</Text>
          <Text style={styles.unreadSub}>{unread.unreadConversations} محادثة</Text>
        </View>
        <View>
          <Text style={styles.eyebrow}>تواصل فريق المركز</Text>
          <Text accessibilityRole="header" style={styles.title}>الرسائل</Text>
        </View>
      </View>

      <TextInput
        value={search}
        onChangeText={setSearch}
        placeholder="ابحث في المحادثات"
        placeholderTextColor={colors.muted}
        style={styles.search}
        textAlign="right"
      />

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.primary} />
        </View>
      ) : error && conversations.length === 0 ? (
        <StateMessage
          title="تعذر تحميل الرسائل"
          message={error}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load(search)} />}
        />
      ) : (
        <>
        {error ? <Text accessibilityRole="alert" style={styles.refreshError}>{error}</Text> : null}
        <FlatList
          data={conversations}
          keyExtractor={(item) => item.id}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}
          contentContainerStyle={styles.list}
          keyboardShouldPersistTaps="handled"
          ListEmptyComponent={
            <Card>
              <Text style={styles.empty}>لا توجد محادثات مطابقة.</Text>
            </Card>
          }
          renderItem={({ item }) => <ConversationCard item={item} />}
        />
        </>
      )}
    </Screen>
  );
}

function ConversationCard({ item }: { item: ConversationListItem }) {
  const patientLabel = item.patientNumber
    ? `${item.patientName || "مريض"} · ${item.patientNumber}`
    : null;

  return (
    <Pressable
      onPress={() =>
        router.push({ pathname: "/(app)/message-detail", params: { id: item.id } })
      }
      style={({ pressed }) => [styles.card, pressed && { opacity: 0.82 }]}
    >
      <View style={styles.cardHeader}>
        {item.unreadCount > 0 ? (
          <View style={styles.badge}>
            <Text style={styles.badgeText}>{item.unreadCount}</Text>
          </View>
        ) : (
          <Text style={styles.arrow}>‹</Text>
        )}
        <View style={{ flex: 1 }}>
          <Text style={[styles.cardTitle, item.unreadCount > 0 && styles.cardTitleUnread]}>
            {item.title}
          </Text>
          {patientLabel ? <Text style={styles.patient}>{patientLabel}</Text> : null}
        </View>
      </View>
      <Text numberOfLines={2} style={styles.preview}>
        {item.lastMessagePreview || "لا توجد رسائل بعد"}
      </Text>
      {item.lastMessageAt ? (
        <Text style={styles.date}>{formatTimestamp(item.lastMessageAt)}</Text>
      ) : null}
    </Pressable>
  );
}

function formatTimestamp(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString("ar-YE", {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
}

const styles = StyleSheet.create({
  header: {
    flexDirection: "row",
    alignItems: "flex-end",
    justifyContent: "space-between",
    gap: spacing.md
  },
  eyebrow: { color: colors.secondary, fontSize: 11, fontWeight: "900", textAlign: "right" },
  title: { color: colors.text, fontSize: 25, fontWeight: "900", textAlign: "right" },
  unreadCard: { backgroundColor: colors.primarySoft, borderRadius: radius.sm, paddingHorizontal: spacing.sm, paddingVertical: spacing.xs },
  unread: { color: colors.primary, fontWeight: "900", fontSize: 12 },
  unreadSub: { color: colors.muted, fontSize: 10, marginTop: 2 },
  search: {
    minHeight: 54,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.md,
    backgroundColor: colors.surface,
    color: colors.text,
    textAlign: "right",
    ...shadow.card
  },
  center: { flex: 1, alignItems: "center", justifyContent: "center" },
  list: { gap: spacing.sm, paddingBottom: spacing.xl },
  card: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    gap: spacing.sm,
    ...shadow.card
  },
  cardHeader: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  cardTitle: { color: colors.text, fontSize: 16, fontWeight: "800", textAlign: "right" },
  cardTitleUnread: { fontWeight: "900" },
  patient: { color: colors.primary, fontSize: 12, marginTop: 3, textAlign: "right" },
  preview: { color: colors.muted, lineHeight: 20, textAlign: "right" },
  date: { color: colors.muted, fontSize: 11, textAlign: "left" },
  arrow: { color: colors.muted, fontSize: 26 },
  badge: {
    minWidth: 27,
    height: 27,
    paddingHorizontal: 7,
    borderRadius: 14,
    backgroundColor: colors.accent,
    alignItems: "center",
    justifyContent: "center"
  },
  badgeText: { color: "#fff", fontWeight: "800", fontSize: 12 },
  empty: { color: colors.muted, textAlign: "center" },
  refreshError: { color: colors.danger, backgroundColor: colors.dangerSoft, borderRadius: radius.sm, padding: spacing.sm, textAlign: "right" }
});
