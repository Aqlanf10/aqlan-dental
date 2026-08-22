export type InventoryItem = {
  id: string;
  name: string;
  category?: string | null;
  quantity: number;
  minQuantity: number;
  unit?: string | null;
  costPerUnit?: number | null;
  batchNumber?: string | null;
  expiryDate?: string | null;
  defaultSupplierId?: string | null;
  isLowStock: boolean;
  minStockLevel?: string | null;
  isBelowMinStockLevel?: boolean;
  purchaseUnit?: string | null;
  consumptionUnit?: string | null;
  imageUrl?: string | null;
  warehouseLocation?: string | null;
  createdAt: string;
};

export type InventoryListResponse = {
  data: InventoryItem[];
  total: number;
  page: number;
  pageSize: number;
  readFallback?: boolean;
  fallbackReason?: string | null;
};

export type InventoryValuation = {
  totalItems: number;
  totalQuantity: number;
  totalValue: number;
  lowStockCount: number;
};

export type ExpiringInventoryItem = {
  id: string;
  name: string;
  category?: string | null;
  quantity: number;
  batchNumber?: string | null;
  expiryDate: string;
  daysUntilExpiry: number;
  isExpired: boolean;
};

export type InventoryAdjustment = {
  id: string;
  previousQuantity: number;
  newQuantity: number;
  delta: number;
  reason?: string | null;
  adjustmentType?: string | null;
  adjustedBy?: string | null;
  purchaseOrderLineItemId?: string | null;
  labOrderId?: string | null;
  createdAt: string;
};

export type InventoryAdjustmentResponse = {
  data: InventoryAdjustment[];
  total: number;
  page: number;
  pageSize: number;
};

export type InventoryItemInput = {
  name: string;
  category?: string | null;
  quantity: number;
  minQuantity: number;
  unit?: string | null;
  costPerUnit?: number | null;
  batchNumber?: string | null;
  expiryDate?: string | null;
  minStockLevel?: number | null;
  purchaseUnit?: string | null;
  consumptionUnit?: string | null;
  imageUrl?: string | null;
  warehouseLocation?: string | null;
};

export type LabInventoryConsumableLine = {
  inventoryItemId: string;
  quantity: number;
};

export type LabInventoryConsumptionResult = {
  labOrderId: string;
  orderNumber?: string | null;
  consumed: Array<{
    inventoryItemId: string;
    itemName: string;
    previousQuantity: number;
    consumedQuantity: number;
    newQuantity: number;
    unit?: string | null;
    costPerUnit?: number | null;
    lineCost: number;
    isLowStock: boolean;
  }>;
  materialCost: number;
  currency: string;
  message: string;
};

export function canUseInventory(role?: string | null): boolean {
  // Mirrors InventoryController [Authorize(Policy = "AdminOnly")].
  return role === "Admin";
}

export function inventoryUnit(item: InventoryItem): string {
  return item.consumptionUnit || item.unit || item.purchaseUnit || "وحدة";
}

export function stockState(item: InventoryItem): "low" | "ok" {
  return item.isLowStock || item.isBelowMinStockLevel ? "low" : "ok";
}
