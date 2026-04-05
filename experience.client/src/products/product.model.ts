export interface Product {
  id: string;
  name: string;
  brand?: string;
  modelNumber?: string;
  serialNumber?: string;
  purchaseDate?: string; // ISO date string (YYYY-MM-DD)
  description?: string;
  manualUrl?: string;
  category?: string;
  createdAt: string;
  updatedAt: string;
}
