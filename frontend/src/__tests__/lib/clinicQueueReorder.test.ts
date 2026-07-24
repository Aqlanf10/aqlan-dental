import { describe, expect, it } from "vitest";
import {
  buildQueueReorderPayload,
  type QueueReorderEntry,
} from "@/lib/clinicQueueReorder";

describe("CORE-F-003 clinic queue reorder payload", () => {
  it("returns a bare array, not an object wrapper", () => {
    const payload = buildQueueReorderPayload(
      { id: "a", sortOrder: 2 },
      { id: "b", sortOrder: 1 },
    );
    expect(Array.isArray(payload)).toBe(true);
    // The backend binds List<ReorderItemRequest>; a wrapper like { orders: [...] }
    // must never be produced again.
    expect(payload).not.toHaveProperty("orders");
  });

  it("preserves the swapped sort orders in server shape", () => {
    const payload = buildQueueReorderPayload(
      { id: "moved", sortOrder: 5 },
      { id: "swapped", sortOrder: 3 },
    );
    expect(payload).toEqual([
      { id: "moved", sortOrder: 5 },
      { id: "swapped", sortOrder: 3 },
    ]);
  });

  it("emits only id and sortOrder fields", () => {
    const moved = { id: "x", sortOrder: 1, extra: "nope" } as unknown as QueueReorderEntry;
    const swapped = { id: "y", sortOrder: 0 } as QueueReorderEntry;
    const payload = buildQueueReorderPayload(moved, swapped);
    expect(Object.keys(payload[0]).sort()).toEqual(["id", "sortOrder"]);
  });
});
