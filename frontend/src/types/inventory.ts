export interface InventoryItem {
  id: string;
  name: string;
  category?: string;
  quantity: number;
  minQuantity: number;
  unit?: string;
  costPerUnit?: number;
  isLowStock: boolean;
  batchNumber?: string;
  expiryDate?: string;
  defaultSupplierId?: string;
  defaultSupplierName?: string;
  createdAt: string;
  // ── YOLO-S4 inventory enhancements ───────────────────────────────────────
  /** Parallel decimal threshold to MinQuantity (int). Null = not configured. */
  minStockLevel?: string | null;
  /** True when minStockLevel is set and Quantity < minStockLevel. */
  isBelowMinStockLevel?: boolean;
  purchaseUnit?: string | null;
  consumptionUnit?: string | null;
  imageUrl?: string | null;
  warehouseLocation?: string | null;
}

export interface CreateInventoryItemRequest {
  name: string;
  category?: string;
  quantity: number;
  minQuantity: number;
  unit?: string;
  costPerUnit?: number;
  // ── YOLO-S4 inventory enhancements (all optional) ────────────────────────
  batchNumber?: string;
  expiryDate?: string;
  defaultSupplierId?: string;
  minStockLevel?: number;
  purchaseUnit?: string;
  consumptionUnit?: string;
  imageUrl?: string;
  warehouseLocation?: string;
}

export interface AdjustQuantityRequest {
  delta: number;
  reason?: string;
}

// ─── Supplier types ─────────────────────────────────────────────────────────────
export interface Supplier {
  id: string;
  name: string;
  contactPerson?: string;
  phone?: string;
  email?: string;
  address?: string;
  notes?: string;
  isActive?: boolean;
  purchaseOrderCount?: number;
  openBillCount?: number;
  totalSpent?: number;
  createdAt?: string;
  /**
   * CORE-XMOD-001: what the clinic owes, one entry per currency. Replaces the single
   * `balance`/`outstandingBills` scalars, which summed YER, SAR and USD into one number.
   */
  balancesByCurrency?: Array<{
    currency: string;
    totalBilled: number;
    totalPaid: number;
    balance: number;
    openBills: number;
  }>;
  /** Legacy YER-only column, kept only for modules that still read it. Do not use for new work. */
  legacyYerBalance?: number;
}

export interface PaginatedResponse<T> {
  data: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CreateSupplierRequest {
  name: string;
  contactPerson?: string;
  phone?: string;
  email?: string;
  address?: string;
  notes?: string;
}

// ─── Purchase Order types ────────────────────────────────────────────────────────
export type PurchaseOrderStatus = 'Draft' | 'Submitted' | 'PartiallyReceived' | 'Received' | 'Cancelled';

export interface PurchaseOrderLineItem {
  id: string;
  purchaseOrderId: string;
  inventoryItemId?: string;
  itemName: string;
  itemDescription?: string;
  quantity: number;
  receivedQuantity: number;
  unitCost: number;
  totalPrice: number;
  sortOrder: number;
}

export interface PurchaseOrder {
  id: string;
  orderNumber: string;
  supplierId: string;
  supplierName?: string;
  status: PurchaseOrderStatus;
  orderDate: string;
  expectedDate?: string;
  receivedDate?: string;
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  notes?: string;
  lineItems: PurchaseOrderLineItem[];
  createdAt: string;
}

export interface CreatePurchaseOrderRequest {
  supplierId: string;
  expectedDate?: string;
  notes?: string;
  taxAmount?: number;
  lineItems: {
    inventoryItemId?: string;
    itemName: string;
    quantity: number;
    unitCost: number;
  }[];
}

export interface ReceivePurchaseOrderRequest {
  lineItems: {
    id: string;
    receivedQuantity: number;
  }[];
}

// ─── Inventory Adjustment type ──────────────────────────────────────────────────
export interface InventoryAdjustment {
  id: string;
  inventoryItemId: string;
  previousQuantity: number;
  newQuantity: number;
  delta: number;
  reason?: string;
  adjustmentType: string;
  createdAt: string;
}
