/**
 * CORE-F-003 — Clinic queue reorder request contract.
 *
 * The backend action `ClinicQueueController.Reorder` binds
 * `[FromBody] List<ReorderItemRequest>`, i.e. a **bare JSON array** of
 * `{ id, sortOrder }`. Posting a wrapped object such as `{ orders: [...] }`
 * fails ASP.NET Core model binding, so `items` binds empty and the endpoint
 * returns `400 "قائمة الترتيب فارغة"`. The drag/up-down reorder then silently
 * reverts. This helper builds the exact array contract the server expects and
 * keeps that shape under test so the wire format cannot drift again.
 */
export interface QueueReorderEntry {
  id: string;
  sortOrder: number;
}

export function buildQueueReorderPayload(
  moved: QueueReorderEntry,
  swapped: QueueReorderEntry,
): QueueReorderEntry[] {
  return [
    { id: moved.id, sortOrder: moved.sortOrder },
    { id: swapped.id, sortOrder: swapped.sortOrder },
  ];
}
