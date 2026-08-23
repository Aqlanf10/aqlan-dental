import type { NotificationItem, NotificationsResponse } from "./types";

type UnknownRecord = Record<string, unknown>;

function record(value: unknown): UnknownRecord | null {
  return value !== null && typeof value === "object" ? value as UnknownRecord : null;
}

function property(source: UnknownRecord, camel: string, pascal: string): unknown {
  return source[camel] ?? source[pascal];
}

function text(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

function number(value: unknown, fallback = 0): number {
  const parsed = typeof value === "number" ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function normalizeNotification(value: unknown): NotificationItem | null {
  const source = record(value);
  if (!source) return null;
  const id = text(property(source, "id", "Id"));
  if (!id) return null;
  return {
    id,
    type: text(property(source, "type", "Type")) || "system",
    title: text(property(source, "title", "Title")) || "إشعار",
    body: text(property(source, "body", "Body")),
    isRead: property(source, "isRead", "IsRead") === true,
    relatedEntity: text(property(source, "relatedEntity", "RelatedEntity")) || null,
    relatedId: text(property(source, "relatedId", "RelatedId")) || null,
    createdAt: text(property(source, "createdAt", "CreatedAt"))
  };
}

export function normalizeNotifications(value: unknown): NotificationsResponse {
  const source = record(value) ?? {};
  const rawData = property(source, "data", "Data");
  const data = Array.isArray(rawData)
    ? rawData.map(normalizeNotification).filter((item): item is NotificationItem => item !== null)
    : [];
  return {
    data,
    total: Math.max(0, number(property(source, "total", "Total"), data.length)),
    unreadCount: Math.max(0, number(property(source, "unreadCount", "UnreadCount"))),
    page: Math.max(1, number(property(source, "page", "Page"), 1)),
    pageSize: Math.max(1, number(property(source, "pageSize", "PageSize"), Math.max(data.length, 1)))
  };
}
