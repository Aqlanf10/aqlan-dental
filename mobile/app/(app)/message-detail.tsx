import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiAssetUrl, apiRequest } from "@/lib/api";
import {
  normalizeConversationDetail,
  normalizeConversationMessage,
  normalizePollMessages
} from "@/lib/messages";
import { markRuntimeAction } from "@/lib/runtimeDiagnostics";
import type { ConversationDetail, ConversationMessage } from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import { useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useEffect, useRef, useState } from "react";
import {
  ActivityIndicator,
  Linking,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  TextInput,
  View
} from "react-native";

const POLL_INTERVAL_MS = 5000;

type RenderAttachment = {
  key: string;
  url: string;
  name: string;
  mimeType?: string | null;
  size?: number;
};

export default function MessageDetailScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id?: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const [conversation, setConversation] = useState<ConversationDetail | null>(null);
  const [text, setText] = useState("");
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const lastSeenRef = useRef<string | null>(null);
  const pollingRef = useRef(false);
  const sendingRef = useRef(false);

  const markAsRead = useCallback(async () => {
    if (!id) return;
    try {
      await apiRequest<void>(`/api/messages/conversations/${id}/read`, { method: "POST" });
    } catch {
      // Reading the conversation must not fail just because the read receipt could not be saved.
    }
  }, [id]);

  const load = useCallback(async () => {
    if (!id) {
      setError("معرّف المحادثة غير موجود.");
      setLoading(false);
      return;
    }

    setError(null);
    try {
      const result = await apiRequest<unknown>(
        `/api/messages/conversations/${id}?page=1&pageSize=50`
      );
      const normalized = normalizeConversationDetail(result);
      if (!normalized) throw new Error("استجابة المحادثة غير مكتملة.");
      setConversation(normalized);
      lastSeenRef.current = newestTimestamp(normalized.messages) ?? normalized.createdAt;
      await markAsRead();
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل المحادثة");
    } finally {
      setLoading(false);
    }
  }, [id, markAsRead]);

  useEffect(() => {
    void load();
  }, [load]);

  useFocusEffect(
    useCallback(() => {
      if (!id) return undefined;
      let active = true;

      const poll = async () => {
        const since = lastSeenRef.current;
        if (!active || !since || pollingRef.current) return;

        pollingRef.current = true;
        try {
          const response = await apiRequest<unknown>(
            `/api/messages/conversations/${id}/poll?since=${encodeURIComponent(since)}`
          );
          const incoming = normalizePollMessages(response);
          if (!active || incoming.length === 0) return;

          setConversation((current) =>
            current
              ? { ...current, messages: mergeMessages(current.messages, incoming) }
              : current
          );

          const newest = newestTimestamp(incoming);
          if (newest && (!lastSeenRef.current || newest > lastSeenRef.current)) {
            lastSeenRef.current = newest;
          }
          await markAsRead();
        } catch {
          // Polling is best-effort. The next cycle or manual refresh can recover.
        } finally {
          pollingRef.current = false;
        }
      };

      const interval = setInterval(() => void poll(), POLL_INTERVAL_MS);
      return () => {
        active = false;
        clearInterval(interval);
      };
    }, [id, markAsRead])
  );

  async function refresh() {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  }

  async function send() {
    const content = text.trim();
    if (!id || !content || sendingRef.current) return;

    sendingRef.current = true;
    setSending(true);
    setError(null);
    markRuntimeAction("إرسال رسالة", id);
    try {
      const response = await apiRequest<unknown>(
        `/api/messages/conversations/${id}/messages`,
        {
          method: "POST",
          body: JSON.stringify({ content })
        }
      );
      const message = normalizeConversationMessage(response);
      if (!message) throw new Error("تم الإرسال لكن استجابة الخادم غير مكتملة. حدّث المحادثة للتأكد.");
      setConversation((current) =>
        current ? { ...current, messages: mergeMessages(current.messages, [message]) } : current
      );
      if (!lastSeenRef.current || message.createdAt > lastSeenRef.current) {
        lastSeenRef.current = message.createdAt;
      }
      setText("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر إرسال الرسالة");
    } finally {
      sendingRef.current = false;
      setSending(false);
    }
  }

  if (loading) {
    return (
      <Screen>
        <ActivityIndicator size="large" color={colors.primary} />
      </Screen>
    );
  }

  if (!conversation) {
    return (
      <Screen>
        <StateMessage
          title="تعذر فتح المحادثة"
          message={error ?? "المحادثة غير موجودة أو لا تملك صلاحية الوصول إليها."}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />}
        />
      </Screen>
    );
  }

  return (
    <Screen
      refreshControl={
        <RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />
      }
    >
      <View>
        <SectionTitle>{conversation.title}</SectionTitle>
        {conversation.patientName ? (
          <Text style={styles.patientMeta}>
            {conversation.patientName}
            {conversation.patientNumber ? ` · ${conversation.patientNumber}` : ""}
          </Text>
        ) : null}
        {conversation.conversationType === "PatientFacing" ? (
          <Text style={styles.patientFacing}>هذه المحادثة مرئية للمريض في بوابته.</Text>
        ) : null}
      </View>

      {error ? <StateMessage title="تنبيه" message={error} /> : null}

      <View style={styles.messages}>
        {conversation.messages.length === 0 ? (
          <Card>
            <Text style={styles.empty}>لا توجد رسائل بعد.</Text>
          </Card>
        ) : (
          conversation.messages.map((message) => (
            <MessageBubble
              key={message.id}
              message={message}
              isMine={message.senderId === user?.id}
            />
          ))
        )}
      </View>

      <View style={styles.composer}>
        <TextInput
          value={text}
          onChangeText={setText}
          placeholder="اكتب رسالتك…"
          placeholderTextColor={colors.muted}
          multiline
          maxLength={2000}
          textAlign="right"
          style={styles.input}
        />
        <Pressable
          accessibilityRole="button"
          disabled={sending || !text.trim()}
          onPress={() => void send()}
          style={({ pressed }) => [
            styles.send,
            (sending || !text.trim()) && styles.sendDisabled,
            pressed && !sending && text.trim() && { opacity: 0.85 }
          ]}
        >
          {sending ? (
            <ActivityIndicator color="#fff" />
          ) : (
            <Text style={styles.sendText}>إرسال</Text>
          )}
        </Pressable>
      </View>
    </Screen>
  );
}

function MessageBubble({
  message,
  isMine
}: {
  message: ConversationMessage;
  isMine: boolean;
}) {
  if (message.isSystemMessage) {
    return (
      <View style={styles.systemBubble}>
        <Text style={styles.systemText}>{message.content}</Text>
      </View>
    );
  }

  const attachments = collectAttachments(message);

  return (
    <View style={[styles.bubbleWrap, isMine ? styles.mineWrap : styles.otherWrap]}>
      <View style={[styles.bubble, isMine ? styles.mine : styles.other]}>
        {!isMine ? <Text style={styles.sender}>{message.senderName}</Text> : null}
        {message.replyToContent ? (
          <View style={styles.replyBox}>
            <Text numberOfLines={2} style={styles.replyText}>
              {message.replyToSenderName ? `${message.replyToSenderName}: ` : ""}
              {message.replyToContent}
            </Text>
          </View>
        ) : null}
        {message.content ? <Text style={styles.messageText}>{message.content}</Text> : null}
        {attachments.length > 0 ? (
          <View style={styles.attachments}>
            {attachments.map((attachment) => (
              <Pressable
                key={attachment.key}
                accessibilityRole="link"
                onPress={() => void Linking.openURL(apiAssetUrl(attachment.url))}
                style={({ pressed }) => [styles.attachment, pressed && { opacity: 0.8 }]}
              >
                <Text style={styles.attachmentIcon}>{attachmentIcon(attachment.mimeType)}</Text>
                <View style={{ flex: 1 }}>
                  <Text numberOfLines={2} style={styles.attachmentName}>
                    {attachment.name}
                  </Text>
                  <Text style={styles.attachmentMeta}>
                    {attachmentLabel(attachment.mimeType)}
                    {attachment.size ? ` · ${formatFileSize(attachment.size)}` : ""}
                  </Text>
                </View>
              </Pressable>
            ))}
          </View>
        ) : null}
        <Text style={styles.messageTime}>{formatTime(message.createdAt)}</Text>
      </View>
    </View>
  );
}

function collectAttachments(message: ConversationMessage): RenderAttachment[] {
  const result: RenderAttachment[] = [];
  const seen = new Set<string>();

  if (message.attachmentUrl) {
    const key = `legacy:${message.attachmentUrl}`;
    seen.add(message.attachmentUrl);
    result.push({
      key,
      url: message.attachmentUrl,
      name: message.attachmentName || "مرفق",
      mimeType: message.attachmentType
    });
  }

  for (const attachment of message.attachments ?? []) {
    if (seen.has(attachment.fileUrl)) continue;
    seen.add(attachment.fileUrl);
    result.push({
      key: attachment.id || attachment.fileUrl,
      url: attachment.fileUrl,
      name: attachment.fileName || "مرفق",
      mimeType: attachment.mimeType,
      size: attachment.fileSize
    });
  }

  return result;
}

function mergeMessages(
  current: ConversationMessage[],
  incoming: ConversationMessage[]
): ConversationMessage[] {
  const byId = new Map(current.map((message) => [message.id, message]));
  for (const message of incoming) byId.set(message.id, message);
  return Array.from(byId.values()).sort(
    (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
  );
}

function newestTimestamp(messages: ConversationMessage[]): string | null {
  let newest: string | null = null;
  for (const message of messages) {
    if (!newest || message.createdAt > newest) newest = message.createdAt;
  }
  return newest;
}

function attachmentIcon(mimeType?: string | null): string {
  const value = mimeType?.toLowerCase() ?? "";
  if (value.startsWith("image/")) return "▣";
  if (value.startsWith("audio/")) return "♪";
  if (value === "application/pdf") return "PDF";
  return "⌕";
}

function attachmentLabel(mimeType?: string | null): string {
  const value = mimeType?.toLowerCase() ?? "";
  if (value.startsWith("image/")) return "صورة";
  if (value.startsWith("audio/")) return "رسالة صوتية";
  if (value === "application/pdf") return "ملف PDF";
  return "مرفق";
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleTimeString("ar-YE", { hour: "2-digit", minute: "2-digit" });
}

const styles = StyleSheet.create({
  patientMeta: { color: colors.primary, marginTop: 4, textAlign: "right" },
  patientFacing: {
    marginTop: spacing.sm,
    padding: spacing.sm,
    borderRadius: radius.sm,
    backgroundColor: colors.warningSoft,
    color: colors.warning,
    textAlign: "right",
    fontWeight: "700"
  },
  messages: { gap: spacing.sm },
  bubbleWrap: { width: "100%" },
  mineWrap: { alignItems: "flex-start" },
  otherWrap: { alignItems: "flex-end" },
  bubble: {
    maxWidth: "86%",
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderWidth: 1
  },
  mine: { backgroundColor: colors.primarySoft, borderColor: colors.primary },
  other: { backgroundColor: colors.surface, borderColor: colors.border },
  sender: { color: colors.primary, fontSize: 12, fontWeight: "800", textAlign: "right" },
  messageText: { color: colors.text, lineHeight: 22, textAlign: "right", marginTop: 2 },
  messageTime: { color: colors.muted, fontSize: 10, marginTop: 5, textAlign: "left" },
  replyBox: {
    marginTop: 4,
    marginBottom: 5,
    padding: spacing.xs,
    borderRadius: radius.sm,
    backgroundColor: colors.background
  },
  replyText: { color: colors.muted, fontSize: 12, textAlign: "right" },
  attachments: { gap: spacing.xs, marginTop: spacing.sm },
  attachment: {
    minHeight: 54,
    flexDirection: "row-reverse",
    alignItems: "center",
    gap: spacing.sm,
    padding: spacing.sm,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.background
  },
  attachmentIcon: {
    minWidth: 30,
    color: colors.primary,
    fontWeight: "900",
    textAlign: "center"
  },
  attachmentName: { color: colors.text, fontWeight: "700", textAlign: "right" },
  attachmentMeta: { color: colors.muted, fontSize: 11, marginTop: 2, textAlign: "right" },
  systemBubble: { alignItems: "center", paddingVertical: spacing.xs },
  systemText: {
    color: colors.muted,
    backgroundColor: colors.background,
    borderRadius: 999,
    paddingHorizontal: spacing.sm,
    paddingVertical: 5,
    fontSize: 11,
    textAlign: "center"
  },
  composer: { gap: spacing.sm, marginTop: spacing.sm },
  input: {
    minHeight: 88,
    maxHeight: 180,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    backgroundColor: colors.surface,
    color: colors.text,
    padding: spacing.md,
    textAlignVertical: "top"
  },
  send: {
    minHeight: 48,
    borderRadius: radius.sm,
    backgroundColor: colors.primary,
    alignItems: "center",
    justifyContent: "center"
  },
  sendDisabled: { opacity: 0.55 },
  sendText: { color: "#fff", fontWeight: "800", fontSize: 16 },
  empty: { color: colors.muted, textAlign: "center" }
});
