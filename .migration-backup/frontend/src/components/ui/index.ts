/**
 * Shared UI primitive barrel.
 * Import via `@/components/ui` to keep call sites tidy.
 */
export { Button } from "./Button";
export type { ButtonProps, ButtonVariant, ButtonSize } from "./Button";

export { Card, CardHeader, CardTitle, CardBody } from "./Card";
export type { CardProps } from "./Card";

export { Badge } from "./Badge";
export type { BadgeProps, BadgeVariant } from "./Badge";

export { Input } from "./Input";
export type { InputProps } from "./Input";

export { SectionHeader } from "./SectionHeader";
export type { SectionHeaderProps } from "./SectionHeader";

export { EmptyState } from "./EmptyState";
export type { EmptyStateProps } from "./EmptyState";

export { LoadingState } from "./LoadingState";
export type { LoadingStateProps } from "./LoadingState";

export { Skeleton, TableSkeleton, CardSkeleton } from "./skeleton";
export { ConfirmDialog } from "./ConfirmDialog";
export { Toaster } from "./toaster";
