import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import type { NotificationItem, NotificationsResponse } from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import React, { useCallback, useEffect, useState } from "react";
import { Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

export default function NotificationsScreen() {
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const [refreshing, setRefreshing] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const params = new URLSearchParams({
        unreadOnly: String(unreadOnly),
        page: "1",
        pageSize: "50"
      });
      const response = await apiRequest<NotificationsResponse>(
        `/api/notifications?${params.toString()}`
      );
      setItems(response.data ?? []);
      setUnreadCount(response.unreadCount ?? 0);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل الإشعارات");
    } finally {
      setLoading(false);
    }
  }, [unreadOnly]);

  useEffect(() => {
    setLoading(true);
    void load();
  }, [load]);

  async function refresh() {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  }

  async function markRead(item: NotificationItem) {
    if (item.isRead) return;
    try {
      await apiRequest<void>(`/api/notifications/${item.id}/read`, { method: "PUT" });
      setItems((current) =>
        unreadOnly
          ? current.filter((candidate) => candidate.id !== item.id)
          : current.map((candidate) =>
              candidate.id === item.id ? { ...candidate, isRead: true } : candidate
            )
      );
      setUnreadCount((count) => Math.max(0, count - 1));
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحديث الإشعار");
    }
  }

  async function markAllRead() {
    try {
      await apiRequest<void>("/api/notifications/read-all", { method: "PUT" });
      setUnreadCount(0);
      setItems((current) =>
        unreadOnly ? [] : current.map((item) => ({ ...item, isRead: true }))
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تعليم الإشعارات كمقروءة");
    }
  }

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View style={styles.header}>
        <View>
          <Text style={styles.count}>{unreadCount} غير مقروء</Text>
          <Text style={styles.hint}>اضغط على الإشعار لتعليمه كمقروء</Text>
        </View>
        <SectionTitle>الإشعارات</SectionTitle>
      </View>

      <View style={styles.actions}>
        <Pressable
          onPress={() => setUnreadOnly((value) => !value)}
          style={[styles.filter, unreadOnly && styles.filterActive]}
        >
          <Text style={[styles.filterText, unreadOnly && styles.filterTextActive]}>
            {unreadOnly ? "عرض الكل" : "غير المقروء فقط"}
          </Text>
        </Pressable>
        <View style={{ flex: 1 }}>
          <PrimaryButton
            title="تعليم الكل كمقروء"
            disabled={unreadCount === 0}
            onPress={() => void markAllRead()}
          />
        </View>
      </View>

      {error ? <StateMessage title="تنبيه" message={error} /> : null}

      {loading ? (
        <Card>
          <Text style={styles.empty}>جارٍ تحميل الإشعارات…</Text>
        </Card>
      ) : items.length === 0 ? (
        <Card>
          <Text style={styles.empty}>
            {unreadOnly ? "لا توجد إشعارات غير مقروءة." : "لا توجد إشعارات حالياً."}
          </Text>
        </Card>
      ) : (
        items.map((item) => (
          <Pressable
            key={item.id}
            onPress={() => void markRead(item)}
            style={({ pressed }) => [
              styles.notification,
              !item.isRead && styles.unreadNotification,
              pressed && { opacity: 0.84 }
            ]}
          >
            <View style={styles.notificationHeader}>
              <Text style={styles.date}>{formatTimestamp(item.createdAt)}</Text>
              <View style={{ flex: 1 }}>
                <Text style={[styles.title, !item.isRead && styles.titleUnread]}>{item.title}</Text>
                <Text style={styles.type}>{notificationTypeLabel(item.type)}</Text>
              </View>
            </View>
            <Text style={styles.body}>{item.body}</Text>
          </Pressable>
        ))
      )}
    </Screen>
  );
}

function notificationTypeLabel(value: string): string {
  const labels: Record<string, string> = {
    appointment: "موعد",
    payment: "مالي",
    lab: "معمل",
    message: "رسالة",
    system: "النظام"
  };
  return labels[value.toLowerCase()] ?? value;
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
    justifyContent: "space-between",
    alignItems: "flex-end",
    gap: spacing.md
  },
  count: { color: colors.primary, fontWeight: "800" },
  hint: { color: colors.muted, fontSize: 11, marginTop: 3 },
  actions: { flexDirection: "row-reverse", alignItems: "center", gap: spacing.sm },
  filter: {
    minHeight: 48,
    paddingHorizontal: spacing.md,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
    alignItems: "center",
    justifyContent: "center"
  },
  filterActive: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  filterText: { color: colors.text, fontWeight: "700" },
  filterTextActive: { color: colors.primary },
  notification: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    backgroundColor: colors.surface,
    padding: spacing.md,
    gap: spacing.sm
  },
  unreadNotification: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  notificationHeader: { flexDirection: "row", alignItems: "flex-start", gap: spacing.md },
  title: { color: colors.text, fontSize: 16, fontWeight: "700", textAlign: "right" },
  titleUnread: { fontWeight: "900" },
  type: { color: colors.primary, fontSize: 11, marginTop: 2, textAlign: "right" },
  date: { color: colors.muted, fontSize: 10 },
  body: { color: colors.text, lineHeight: 22, textAlign: "right" },
  empty: { color: colors.muted, textAlign: "center" }
});
