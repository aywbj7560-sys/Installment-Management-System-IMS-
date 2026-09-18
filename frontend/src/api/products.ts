import { apiRequest } from './client';
import type { Product, ProductPage, ProductQuery, ProductRequest } from '../types/products';

export function getProducts(query: ProductQuery, token: string) {
  const params = new URLSearchParams();
  if (query.search) params.set('search', query.search);
  if (query.isActive !== undefined) params.set('isActive', String(query.isActive));
  params.set('page', String(query.page ?? 1));
  params.set('pageSize', String(query.pageSize ?? 10));
  return apiRequest<ProductPage>(`/api/products?${params}`, {}, token);
}
export const getProduct = (id: number, token: string) => apiRequest<Product>(`/api/products/${id}`, {}, token);
export const createProduct = (request: ProductRequest, token: string) => apiRequest<Product>('/api/products', { method: 'POST', body: JSON.stringify(request) }, token);
export const updateProduct = (id: number, request: ProductRequest, token: string) => apiRequest<Product>(`/api/products/${id}`, { method: 'PUT', body: JSON.stringify(request) }, token);
