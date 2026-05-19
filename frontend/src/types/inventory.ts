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
  totalSpent?: number;
  createdAt?: string;
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
