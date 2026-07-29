export interface DashboardStats {
  appointmentsToday:        number;
  newPatientsToday:         number;
  totalPatients:            number;
  activeOrthoCases:         number;
  pendingLabOrders:         number;
  overdueContractsCount:    number;
  totalRevenueMTD:          number;
  // Queue & Booking integration (Sprint 5)
  queueWaitingCount?:       number;
  pendingBookingRequestsCount?: number;
  todayArrivedCount?:       number;
}
