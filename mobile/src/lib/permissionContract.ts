export const OPERATIONAL_PERMISSION = {
  appointments: {
    create: "appointments.create",
    edit: "appointments.edit",
    delete: "appointments.delete"
  },
  patients: {
    edit: "patients.edit"
  },
  visits: {
    edit: "visits.edit",
    delete: "visits.delete"
  },
  clinicQueue: {
    create: "clinic_queue.create",
    edit: "clinic_queue.edit",
    delete: "clinic_queue.delete"
  }
} as const;

/**
 * Mirrors contracts/permission-action-map.json on main (GOLIVE-PERM-001).
 * Queue state transitions (call, recall, start, enter-room, complete, no-show,
 * notify, reorder, priority, room) are EDIT operations, not create/approve.
 */
export type OperationalPermission =
  (typeof OPERATIONAL_PERMISSION)[keyof typeof OPERATIONAL_PERMISSION][keyof (typeof OPERATIONAL_PERMISSION)[keyof typeof OPERATIONAL_PERMISSION]];
