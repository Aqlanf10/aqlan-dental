import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ErrorBoundary } from '@/components/shared/ErrorBoundary';
import type { ReactNode } from 'react';

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Component that renders children without error */
function GoodChild(): ReactNode {
  return <span data-testid="good-child">Hello</span>;
}

/** Component that throws synchronously during render */
function ThrowComponent(): ReactNode {
  throw new Error('Test error boundary');
}

/** Wrapper that swallows the React error boundary console output */
function renderWithBoundary(ui: ReactNode) {
  const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
  try {
    render(ui);
  } finally {
    spy.mockRestore();
  }
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ErrorBoundary', () => {
  it('renders children when no error occurs', () => {
    render(
      <ErrorBoundary>
        <GoodChild />
      </ErrorBoundary>
    );
    expect(screen.getByTestId('good-child')).toHaveTextContent('Hello');
  });

  it('catches errors and displays fallback UI', () => {
    renderWithBoundary(
      <ErrorBoundary>
        <ThrowComponent />
      </ErrorBoundary>
    );

    // The Arabic fallback text should be present
    expect(screen.getByText('حدث خطأ غير متوقع')).toBeInTheDocument();
    // A retry button should be shown
    expect(screen.getByText('إعادة المحاولة')).toBeInTheDocument();
  });

  it('fallback contains error-related text (Arabic or English)', () => {
    renderWithBoundary(
      <ErrorBoundary>
        <ThrowComponent />
      </ErrorBoundary>
    );

    // "خطأ" means "error" in Arabic — the word should appear somewhere
    const bodyText = document.body.textContent ?? '';
    expect(bodyText).toContain('خطأ');
  });

  it('renders custom fallback when provided', () => {
    renderWithBoundary(
      <ErrorBoundary fallback={<div data-testid="custom-fallback">Custom Error</div>}>
        <ThrowComponent />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('custom-fallback')).toHaveTextContent('Custom Error');
    // The default fallback should NOT be shown
    expect(screen.queryByText('حدث خطأ غير متوقع')).not.toBeInTheDocument();
  });
});
