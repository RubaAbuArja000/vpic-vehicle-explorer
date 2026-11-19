import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Make {
  id: number;
  name: string;
}

export interface VehicleType {
  id: number;
  name: string;
}

export interface Model {
  id: number;
  name: string;
  vehicleType: string | null;
}

/** Talks only to our own .NET API (same origin), never to vPIC directly. */
@Injectable({ providedIn: 'root' })
export class VehicleApiService {
  private readonly http = inject(HttpClient);

  searchMakes(search: string, limit = 20): Observable<Make[]> {
    const params = new HttpParams().set('search', search).set('limit', limit);
    return this.http.get<Make[]>('/api/makes', { params });
  }

  getVehicleTypes(makeId: number): Observable<VehicleType[]> {
    return this.http.get<VehicleType[]>(`/api/makes/${makeId}/vehicle-types`);
  }

  getModels(makeId: number, year: number, vehicleType?: string | null): Observable<Model[]> {
    let params = new HttpParams().set('year', year);
    if (vehicleType) {
      params = params.set('vehicleType', vehicleType);
    }
    return this.http.get<Model[]>(`/api/makes/${makeId}/models`, { params });
  }
}
