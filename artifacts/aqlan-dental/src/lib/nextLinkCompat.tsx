/**
 * Compatibility shim: replaces next/link for the Vite/wouter port.
 * Renders a standard <a> tag that works with wouter's client-side routing.
 */
import React from "react";
import { useLocation } from "wouter";

interface LinkProps extends React.AnchorHTMLAttributes<HTMLAnchorElement> {
  href: string;
  children?: React.ReactNode;
  prefetch?: boolean; // ignored — no-op for compat
  replace?: boolean;
}

export default function Link({ href, children, prefetch: _prefetch, replace, onClick, ...props }: LinkProps) {
  const [, setLocation] = useLocation();

  const handleClick = (e: React.MouseEvent<HTMLAnchorElement>) => {
    // Only intercept left-clicks without modifier keys on same-origin paths
    if (
      !e.defaultPrevented &&
      e.button === 0 &&
      !e.metaKey &&
      !e.ctrlKey &&
      !e.shiftKey &&
      !e.altKey &&
      href &&
      !href.startsWith("http") &&
      !href.startsWith("mailto:") &&
      !href.startsWith("tel:")
    ) {
      e.preventDefault();
      if (replace) {
        setLocation(href, { replace: true });
      } else {
        setLocation(href);
      }
    }
    onClick?.(e);
  };

  return (
    <a href={href} onClick={handleClick} {...props}>
      {children}
    </a>
  );
}
