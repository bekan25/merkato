import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface MerkatoProduct {
  id: number;
  sellerId: number;
  name: string;
  priceEtb: number;
  location: string;
  category: string;
  emoji?: string;
  imageUrl?: string | null;
  isActive: boolean;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5000/api';

  getProducts(search = '', category = ''): Observable<MerkatoProduct[]> {
    let params = new HttpParams();
    if (search.trim()) params = params.set('search', search.trim());
    if (category && category !== 'All') params = params.set('category', category);
    return this.http.get<MerkatoProduct[]>(`${this.baseUrl}/products`, { params });
  }

  createProduct(payload: { name: string; priceEtb: number; location: string; category: string; emoji: string }): Observable<MerkatoProduct> {
    return this.http.post<MerkatoProduct>(`${this.baseUrl}/products`, payload);
  }
}
