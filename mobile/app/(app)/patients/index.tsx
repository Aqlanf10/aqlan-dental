import { useSession } from "@/auth/SessionProvider";
import { Card, PrimaryButton, Screen, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import type { PaginatedResponse, PatientListItem } from "@/lib/types";
import { colors, radius, spacing } from "@/theme";
import { router } from "expo-router";
import React, { useCallback, useEffect, useState } from "react";
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

export default function PatientsScreen() {
  const { user } = useSession();
  const [query, setQuery] = useState("");
  const [patients, setPatients] = useState<PatientListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const canCreate = user?.role === "Admin" || user?.role === "Reception";

  const load = useCallback(async (search: string) => {
    setError(null);
    try {
      const params = new URLSearchParams({
        page: "1",
        pageSize: "50",
        status: "active"
      });
      if (search.trim()) params.set("search", search.trim());

      const result = await apiRequest<PaginatedResponse<PatientListItem>>(
        `/api/patients?${params.toString()}`
      );
      setPatients(result.data ?? []);
      setTotalCount(result.totalCount ?? 0);
    } catch (err) {
      setError(err instanceof Error ? err.message : "تعذر تحميل المرضى");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const handle = setTimeout(() => {
      void load(query);
    }, 300);
    return () => clearTimeout(handle);
  }, [load, query]);

  async function refresh() {
    setRefreshing(true);
    try {
      await load(query);
    } finally {
      setRefreshing(false);
    }
  }

  return (
    <Screen scroll={false}>
      <View style={styles.header}>
        <Text style={styles.count}>{totalCount} مريض</Text>
        <Text style={styles.title}>المرضى</Text>
      </View>

      {canCreate ? (
        <PrimaryButton title="إضافة مريض" onPress={() => router.push("/(app)/patients/new")} />
      ) : null}

      <TextInput
        value={query}
        onChangeText={setQuery}
        placeholder="ابحث بالاسم أو رقم الملف أو الهاتف"
        placeholderTextColor={colors.muted}
        style={styles.search}
        textAlign="right"
      />

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.primary} />
        </View>
      ) : error && patients.length === 0 ? (
        <StateMessage
          title="تعذر تحميل المرضى"
          message={error}
          action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load(query)} />}
        />
      ) : (
        <FlatList
          data={patients}
          keyExtractor={(item) => item.id}
          refreshControl={
            <RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />
          }
          contentContainerStyle={styles.list}
          keyboardShouldPersistTaps="handled"
          ListEmptyComponent={
            <Card>
              <Text style={styles.empty}>لا توجد نتائج مطابقة.</Text>
            </Card>
          }
          renderItem={({ item }) => (
            <Pressable
              onPress={() =>
                router.push({ pathname: "/(app)/patients/[id]", params: { id: item.id } })
              }
              style={({ pressed }) => [styles.patientCard, pressed && { opacity: 0.8 }]}
            >
              <View style={{ flex: 1 }}>
                <Text style={styles.patientName}>{item.fullName}</Text>
                <Text style={styles.patientMeta}>
                  {item.patientNumber}
                  {item.age ? ` · ${item.age} سنة` : ""}
                </Text>
                {item.primaryDoctorName ? (
                  <Text style={styles.patientMeta}>د. {item.primaryDoctorName}</Text>
                ) : null}
              </View>
              <Text style={styles.chevron}>‹</Text>
            </Pressable>
          )}
        />
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  header: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "baseline"
  },
  title: { color: colors.text, fontSize: 24, fontWeight: "800", textAlign: "right" },
  count: { color: colors.muted, fontSize: 13 },
  search: {
    minHeight: 48,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.md,
    backgroundColor: colors.surface,
    color: colors.text
  },
  center: { flex: 1, alignItems: "center", justifyContent: "center" },
  list: { gap: spacing.sm, paddingBottom: spacing.xl },
  patientCard: {
    minHeight: 82,
    flexDirection: "row-reverse",
    alignItems: "center",
    gap: spacing.md,
    padding: spacing.md,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md
  },
  patientName: { color: colors.text, fontSize: 17, fontWeight: "700", textAlign: "right" },
  patientMeta: { color: colors.muted, fontSize: 13, marginTop: 4, textAlign: "right" },
  chevron: { color: colors.muted, fontSize: 28 },
  empty: { color: colors.muted, textAlign: "center" }
});
