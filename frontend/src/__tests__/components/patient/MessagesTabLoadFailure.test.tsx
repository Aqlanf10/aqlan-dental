import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MessagesTab } from "@/components/patient/tabs/MessagesTab";

const mocks = vi.hoisted(() => ({
  createConversationMutate: vi.fn(),
  markAsReadMutate: vi.fn(),
  sendMessageMutate: vi.fn(),
  useConversation: vi.fn(),
  useSendMessage: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("@/stores/authStore", () => ({
  useAuthStore: (selector: (state: { user: { id: string } }) => unknown) =>
    selector({ user: { id: "staff-1" } }),
}));

vi.mock("@/stores/toastStore", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock("@/hooks/useMessaging", () => ({
  useConversation: (conversationId: string | null) => mocks.useConversation(conversationId),
  useInternalPatientConversation: () => ({
    mutate: mocks.createConversationMutate,
    isPending: false,
  }),
  useMarkAsRead: () => ({ mutate: mocks.markAsReadMutate }),
  useSendMessage: (conversationId: string) => {
    mocks.useSendMessage(conversationId);
    return {
      mutate: mocks.sendMessageMutate,
      isPending: false,
    };
  },
}));

describe("MessagesTab honest conversation loading", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.defineProperty(Element.prototype, "scrollIntoView", {
      configurable: true,
      value: vi.fn(),
    });
    mocks.useConversation.mockReturnValue({
      data: null,
      isLoading: false,
      error: null,
    });
    mocks.createConversationMutate.mockImplementation(
      (_patientId: string, options: { onError: (error: unknown) => void }) => {
        options.onError({
          isAxiosError: true,
          response: {
            status: 503,
            data: { message: "خدمة الرسائل غير متاحة حالياً" },
          },
        });
      },
    );
  });

  it("shows a retryable Arabic error instead of a false empty conversation", async () => {
    render(<MessagesTab patientId="patient-1" />);

    expect(await screen.findByText("خدمة الرسائل غير متاحة حالياً")).toBeInTheDocument();
    expect(screen.getByText(/لم نعتبر فشل الطلب/)).toBeInTheDocument();
    expect(screen.queryByText("لا توجد رسائل")).not.toBeInTheDocument();
    expect(screen.queryByPlaceholderText("اكتب رسالة...")).not.toBeInTheDocument();
    expect(mocks.useSendMessage).toHaveBeenCalledWith("");

    const initialAttempts = mocks.createConversationMutate.mock.calls.length;
    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    await waitFor(() =>
      expect(mocks.createConversationMutate.mock.calls.length).toBeGreaterThan(initialAttempts),
    );
  });

  it("enables the composer only after a valid conversation is loaded", async () => {
    mocks.createConversationMutate.mockImplementation(
      (_patientId: string, options: { onSuccess: (data: { id: string }) => void }) => {
        options.onSuccess({ id: "conversation-1" });
      },
    );
    mocks.useConversation.mockImplementation((conversationId: string | null) => ({
      data: conversationId
        ? {
            id: conversationId,
            conversationType: "StaffToPatient",
            messages: [],
          }
        : null,
      isLoading: false,
      error: null,
    }));

    render(<MessagesTab patientId="patient-1" />);

    expect(await screen.findByPlaceholderText("اكتب رسالة...")).toBeInTheDocument();
    expect(mocks.useSendMessage).toHaveBeenCalledWith("conversation-1");
    expect(screen.getByText("لا توجد رسائل")).toBeInTheDocument();
  });
});
