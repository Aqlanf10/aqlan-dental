/**
 * Compatibility shim: replaces next/image for the Vite port.
 * Renders a standard <img> element with the same props interface.
 */
import React from "react";

interface ImageProps extends React.ImgHTMLAttributes<HTMLImageElement> {
  src: string;
  alt: string;
  width?: number;
  height?: number;
  fill?: boolean;
  priority?: boolean; // ignored — no-op
  quality?: number;   // ignored — no-op
  placeholder?: string;
  blurDataURL?: string;
  unoptimized?: boolean;
  sizes?: string;
  onLoadingComplete?: (img: HTMLImageElement) => void;
  objectFit?: React.CSSProperties["objectFit"];
  objectPosition?: React.CSSProperties["objectPosition"];
  layout?: string; // legacy prop — ignored
  loader?: (args: { src: string; width: number; quality?: number }) => string;
}

export default function Image({
  src,
  alt,
  width,
  height,
  fill,
  priority: _priority,
  quality: _quality,
  placeholder: _placeholder,
  blurDataURL: _blurDataURL,
  unoptimized: _unoptimized,
  onLoadingComplete,
  objectFit,
  objectPosition,
  layout: _layout,
  loader,
  style,
  ...props
}: ImageProps) {
  const resolvedSrc = loader
    ? loader({ src, width: width ?? 0 })
    : src;

  const computedStyle: React.CSSProperties = {
    ...(fill ? { position: "absolute", inset: 0, width: "100%", height: "100%", objectFit: objectFit ?? "cover" } : {}),
    ...(objectFit ? { objectFit } : {}),
    ...(objectPosition ? { objectPosition } : {}),
    ...style,
  };

  return (
    <img
      src={resolvedSrc}
      alt={alt}
      width={fill ? undefined : width}
      height={fill ? undefined : height}
      style={Object.keys(computedStyle).length ? computedStyle : undefined}
      onLoad={onLoadingComplete ? (e) => onLoadingComplete(e.currentTarget) : undefined}
      {...props}
    />
  );
}
