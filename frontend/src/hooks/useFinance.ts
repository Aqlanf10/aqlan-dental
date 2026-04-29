import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/lib/api";
import type {
  Contract,
  Payment,
  FinanceSummary,
  CreateContractRequest,
  UpdateContractRequest,
  CreatePaymentRequest,
  PaginatedResponse,
  ContractFilters,
  PaymentFilters,
  OverdueContract,
  RevenueBySpecialty,
  RevenueByDoctor,
  DailyReport,
  MonthlyReport,
  ReceiptData,
  Expense,
  CreateExpenseRequest,
  UpdateExpenseRequest,
  ExpenseSummary,
  ProfitLoss,
} from "@/types/finance";

// ─── Contracts ──────────────────────────────────────────────────────────────

/** Hook: Fetch paginated contract list */
export function useContracts(filters: ContractFilters = {}) {
  return useQuery({
    queryKey: ["contracts", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.page) params.set("page", String(filters.page));
      if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
      if (filters.patientId) params.set("patientId", filters.patientId);
      if (filters.status) params.set("status", filters.status);
      if (filters.specialty) params.set("specialty", filters.specialty);
      if (filters.search) params.set("search", filters.search);

      const { data } = await api.get<PaginatedResponse<Contract>>(
        `/api/contracts?${params.toString()}`
      );
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Fetch single contract */
export function useContract(id: string | null) {
  return useQuery({
    queryKey: ["contract", id],
    queryFn: async () => {
      const { data } = await api.get<Contract>(`/api/contracts/${id}`);
      return data;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

/** Hook: Create contract */
export function useCreateContract() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (req: CreateContractRequest) => {
      const { data } = await api.post<{ id: string }>("/api/contracts", req);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["contracts"] });
      queryClient.invalidateQueries({ queryKey: ["finance-summary"] });
    },
  });
}

/** Hook: Update contract */
export function useUpdateContract() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, ...req }: UpdateContractRequest & { id: string }) => {
      const { data } = await api.put<Contract>(`/api/contracts/${id}`, req);
      return data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["contract", variables.id] });
      queryClient.invalidateQueries({ queryKey: ["contracts"] });
      queryClient.invalidateQueries({ queryKey: ["finance-summary"] });
    },
  });
}

/** Hook: Delete contract */
export function useDeleteContract() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.delete(`/api/contracts/${id}`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["contracts"] });
      queryClient.invalidateQueries({ queryKey: ["finance-summary"] });
    },
  });
}

// ─── Payments ───────────────────────────────────────────────────────────────

/** Hook: Fetch paginated payments */
export function usePayments(filters: PaymentFilters = {}) {
  return useQuery({
    queryKey: ["payments", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.page) params.set("page", String(filters.page));
      if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
      if (filters.patientId) params.set("patientId", filters.patientId);
      if (filters.contractId) params.set("contractId", filters.contractId);
      if (filters.paymentMethod) params.set("paymentMethod", filters.paymentMethod);
      if (filters.specialty) params.set("specialty", filters.specialty);
      if (filters.from) params.set("from", filters.from);
      if (filters.to) params.set("to", filters.to);

      const { data } = await api.get<PaginatedResponse<Payment>>(
        `/api/payments?${params.toString()}`
      );
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Fetch single payment */
export function usePayment(id: string | null) {
  return useQuery({
    queryKey: ["payment", id],
    queryFn: async () => {
      const { data } = await api.get<Payment>(`/api/payments/${id}`);
      return data;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

/** Hook: Create payment */
export function useCreatePayment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (req: CreatePaymentRequest) => {
      const { data } = await api.post<Payment>("/api/payments", req);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["payments"] });
      queryClient.invalidateQueries({ queryKey: ["contracts"] });
      queryClient.invalidateQueries({ queryKey: ["finance-summary"] });
    },
  });
}

// ─── Finance Summary & Analytics ────────────────────────────────────────────

/** Hook: Finance summary stats */
export function useFinanceSummary() {
  return useQuery({
    queryKey: ["finance-summary"],
    queryFn: async () => {
      const { data } = await api.get<FinanceSummary>("/api/finance/summary");
      return data;
    },
    staleTime: 60_000,
  });
}

/** Hook: Overdue contracts */
export function useOverdueContracts() {
  return useQuery({
    queryKey: ["overdue-contracts"],
    queryFn: async () => {
      const { data } = await api.get<OverdueContract[]>("/api/finance/overdue");
      return data;
    },
    staleTime: 60_000,
  });
}

/** Hook: Receipt data for printing */
export function useReceipt(paymentId: string | null) {
  return useQuery({
    queryKey: ["receipt", paymentId],
    queryFn: async () => {
      const { data } = await api.get<ReceiptData>(`/api/receipts/${paymentId}`);
      return data;
    },
    enabled: !!paymentId,
    staleTime: 120_000,
  });
}

/** Hook: Revenue by specialty */
export function useFinanceBySpecialty(from: string, to: string) {
  return useQuery({
    queryKey: ["finance-by-specialty", from, to],
    queryFn: async () => {
      const { data } = await api.get<RevenueBySpecialty[]>(
        `/api/finance/by-specialty?from=${from}&to=${to}`
      );
      return data;
    },
    enabled: !!from && !!to,
    staleTime: 60_000,
  });
}

/** Hook: Revenue by doctor */
export function useFinanceByDoctor(from: string, to: string) {
  return useQuery({
    queryKey: ["finance-by-doctor", from, to],
    queryFn: async () => {
      const { data } = await api.get<RevenueByDoctor[]>(
        `/api/finance/by-doctor?from=${from}&to=${to}`
      );
      return data;
    },
    enabled: !!from && !!to,
    staleTime: 60_000,
  });
}

/** Hook: Daily report */
export function useDailyReport(date: string | null) {
  return useQuery({
    queryKey: ["daily-report", date],
    queryFn: async () => {
      const { data } = await api.get<DailyReport>(
        `/api/finance/daily-report?date=${date}`
      );
      return data;
    },
    enabled: !!date,
    staleTime: 120_000,
  });
}

/** Hook: Monthly report */
export function useMonthlyReport(year: number | null, month: number | null) {
  return useQuery({
    queryKey: ["monthly-report", year, month],
    queryFn: async () => {
      const { data } = await api.get<MonthlyReport>(
        `/api/finance/monthly-report?year=${year}&month=${month}`
      );
      return data;
    },
    enabled: !!year && !!month,
    staleTime: 120_000,
  });
}

// ─── Expenses ──────────────────────────────────────────────────────────────

export interface ExpenseFilters {
  page?: number;
  pageSize?: number;
  category?: string;
  from?: string;
  to?: string;
}

/** Hook: Fetch paginated expenses */
export function useExpenses(filters: ExpenseFilters = {}) {
  return useQuery({
    queryKey: ["expenses", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.page) params.set("page", String(filters.page));
      if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
      if (filters.category) params.set("category", filters.category);
      if (filters.from) params.set("from", filters.from);
      if (filters.to) params.set("to", filters.to);

      const { data } = await api.get<PaginatedResponse<Expense>>(
        `/api/expenses?${params.toString()}`
      );
      return data;
    },
    staleTime: 30_000,
  });
}

/** Hook: Create expense */
export function useCreateExpense() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (req: CreateExpenseRequest) => {
      const { data } = await api.post<Expense>("/api/expenses", req);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["expenses"] });
      queryClient.invalidateQueries({ queryKey: ["expense-summary"] });
    },
  });
}

/** Hook: Update expense */
export function useUpdateExpense() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, ...req }: UpdateExpenseRequest & { id: string }) => {
      const { data } = await api.put<Expense>(`/api/expenses/${id}`, req);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["expenses"] });
      queryClient.invalidateQueries({ queryKey: ["expense-summary"] });
    },
  });
}

/** Hook: Delete expense */
export function useDeleteExpense() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete(`/api/expenses/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["expenses"] });
      queryClient.invalidateQueries({ queryKey: ["expense-summary"] });
    },
  });
}

/** Hook: Expense summary */
export function useExpenseSummary() {
  return useQuery({
    queryKey: ["expense-summary"],
    queryFn: async () => {
      const { data } = await api.get<ExpenseSummary>("/api/expenses/summary");
      return data;
    },
    staleTime: 60_000,
  });
}

/** Hook: Profit & Loss */
export function useProfitLoss(from: string, to: string) {
  return useQuery({
    queryKey: ["profit-loss", from, to],
    queryFn: async () => {
      const { data } = await api.get<ProfitLoss>(`/api/finance/profit-loss?from=${from}&to=${to}`);
      return data;
    },
    enabled: !!from && !!to,
    staleTime: 60_000,
  });
}
