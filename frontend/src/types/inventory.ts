export interface InventoryItem {
  id: string;
  name: string;
  category?: string;
  quantity: number;
  minQuantity: number;
  unit?: string;
  costPerUnit?: number;
  isLowStock: boolean;
  createdAt: string;
}

export interface CreateInventoryItemRequest {
  name: string;
  category?: string;
  quantity: number;
  minQuantity: number;
  unit?: string;
  costPerUnit?: number;
}

export interface AdjustQuantityRequest {
  delta: number;
  reason?: string;
}
