"use client";

import { Component, ErrorInfo, ReactNode } from "react";
import { AlertTriangle, RefreshCw } from "lucide-react";

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false, error: null };

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("[ErrorBoundary]", error, info.componentStack);
  }

  reset = () => this.setState({ hasError: false, error: null });

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) return this.props.fallback;

      return (
        <div className="flex flex-col items-center justify-center min-h-[300px] gap-4 p-8 text-center">
          <AlertTriangle className="w-12 h-12 text-amber-500" />
          <div>
            <p className="font-semibold text-gray-800 text-lg">حدث خطأ غير متوقع</p>
            <p className="text-sm text-gray-500 mt-1">
              {this.state.error?.message ?? "يرجى المحاولة مرة أخرى"}
            </p>
          </div>
          <button
            onClick={this.reset}
            className="flex items-center gap-2 px-4 py-2 bg-cyan-700 text-white rounded-lg text-sm hover:bg-cyan-800 transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            إعادة المحاولة
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}
