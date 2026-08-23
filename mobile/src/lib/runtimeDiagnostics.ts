export type RuntimeAction = {
  label: string;
  target?: string;
  occurredAt: string;
};

let lastAction: RuntimeAction | null = null;

export function markRuntimeAction(label: string, target?: string): void {
  lastAction = { label, target, occurredAt: new Date().toISOString() };
}

export function readLastRuntimeAction(): RuntimeAction | null {
  return lastAction;
}

export function clearLastRuntimeAction(): void {
  lastAction = null;
}
