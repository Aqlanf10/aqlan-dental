import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import type { ConversationDetail, ConversationMessage } from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import { useLocalSearchParams } from "expo-router";
import React, { useCallback, useEffect, useState } from "react";
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View
} from "react-native";

export default function MessageDetailScreen() {
  const { user } = useSession();
  const params = useLocalSearchParams<{ id?: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const [conversation, setConversation] = useState<ConversationDetail | null>(null);
  const [text, setText] = useState("");
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) {
      setError("معرّف المحادثة غير موجود.");
      setLoading(false);
      return;
    }

    setError(null);
    try {
      const result = await apiRequest<ConversationDetail>(
        `/api/messages/conversations/${id}?page=1&pageSize=50`
      );
      setConversation(result);
      try {
        await apiRequest<void>(`/api/messages/conversations/${id}/read`, { method: "POST" });
      } catch {
        // Reading the conversation must not fail just because the read receipt could not be saved.
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل المحادثة");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  async function send() {
    const content = text.trim();
    if (!id || !content || sending) return;

    setSending(true);
    setError(null);
    try {
      const message = await apiRequest<ConversationMessage>(
        `/api/messages/conversations/${id}/messages`,
        {
          method: "POST",
          body: JSON.stringify({ content })
        }
      );
      setConversation((current) =>
        current ? { ...current, messages: [...current.messages, message] } : current
      );
      setText("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر إرسال الرسالة");
    } finally {
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
    <Screen>
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
            pressed && !sending && Boolean(text.trim()) && { opacity: 0.85 }
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
        <Text style={styles.messageText}>{message.content}</Text>
        <Text style={styles.messageTime}>{formatTime(message.createdAt)}</Text>
      </View>
    </View>
  );
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
