export interface Product {
  id: string;
  name: string | null;
  price: number | null;
  brand: string | null;
  modelNumber: string | null;
  serialNumber: string | null;

  // ISO 8601 datetime string, e.g. "2024-01-15T00:00:00Z"
  purchaseDate: string | null;

  category: string | null;
  description: string | null;

  manualUrl: string | null;

  createdAt: string;
  updatedAt: string | null;
}

export interface ODataResponse<T> {
  value: T[];
}
