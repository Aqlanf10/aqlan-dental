import { forwardRef } from "react";
import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/utils";

/**
 * Card — white surface primitive matching the repeated inline `cardStyle`
 * object used across dashboard pages:
 *   background #fff, rounded ~12px, subtle border (#e8f0f9 → border-clinic-blue-100),
 *   soft shadow (shadow-card token), padding ~20px (p-5).
 *
 * Set `padded={false}` for edge-to-edge content (the old `cardNoPadStyle`).
 * Compose `CardHeader` / `CardTitle` / `CardBody` for structured cards, or pass
 * a `title` prop for the simple case.
 */
export interface CardProps extends Omit<HTMLAttributes<HTMLDivElement>, "title"> {
  /** Optional title rendered in a header band. */
  title?: ReactNode;
  /** Apply default inner padding (p-5). Defaults to true. */
  padded?: boolean;
}

export const Card = forwardRef<HTMLDivElement, CardProps>(
  ({ title, padded = true, className, children, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(
        "bg-white rounded-xl border border-clinic-blue-100 shadow-card",
        !title && padded && "p-5",
        !padded && "overflow-hidden",
        className,
      )}
      {...props}
    >
      {title ? (
        <>
          <CardTitle className="px-5 pt-5">{title}</CardTitle>
          <div className={cn(padded && "p-5 pt-3")}>{children}</div>
        </>
      ) : (
        children
      )}
    </div>
  ),
);
Card.displayName = "Card";

export function CardHeader({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn("flex items-center justify-between px-5 pt-5 pb-3", className)}
      {...props}
    />
  );
}

export function CardTitle({ className, ...props }: HTMLAttributes<HTMLHeadingElement>) {
  return (
    <h3
      className={cn("text-base font-semibold text-clinic-navy", className)}
      {...props}
    />
  );
}

export function CardBody({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("p-5", className)} {...props} />;
}
