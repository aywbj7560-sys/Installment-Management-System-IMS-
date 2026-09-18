export interface Product {
  productId: number;
  productCode: string;
  name: string;
  description: string | null;
  cashPrice: number;
  installmentPrice: number | null;
  isActive: boolean;
  createdAt: string;
}

export interface ProductPage { items: Product[]; totalCount: number; page: number; pageSize: number }
export interface ProductRequest {
  productCode: string;
  name: string;
  description: string | null;
  cashPrice: number;
  installmentPrice: number | null;
  isActive: boolean;
}
export interface ProductQuery { search?: string; isActive?: boolean; page?: number; pageSize?: number }
