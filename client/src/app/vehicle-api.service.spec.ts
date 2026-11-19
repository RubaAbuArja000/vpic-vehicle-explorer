import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, throwError, toArray } from 'rxjs';
import { LoadState, toLoadState } from './load-state';
import { VehicleApiService } from './vehicle-api.service';

describe('VehicleApiService', () => {
  let api: VehicleApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(VehicleApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('omits vehicleType when no type is selected', () => {
    api.getModels(474, 2015, null).subscribe();
    const req = http.expectOne((r) => r.url === '/api/makes/474/models');
    expect(req.request.params.get('year')).toBe('2015');
    expect(req.request.params.has('vehicleType')).toBeFalse();
    req.flush([]);
  });

  it('passes vehicleType through when selected', () => {
    api.getModels(474, 2015, 'Passenger Car').subscribe();
    const req = http.expectOne((r) => r.url === '/api/makes/474/models');
    expect(req.request.params.get('vehicleType')).toBe('Passenger Car');
    req.flush([]);
  });
});

describe('toLoadState', () => {
  it('emits loading then ok', async () => {
    const states = await firstValueFrom(toLoadState(of([1, 2])).pipe(toArray()));
    expect(states).toEqual([{ status: 'loading' }, { status: 'ok', data: [1, 2] }]);
  });

  it('turns a problem-details error into a readable message instead of erroring', async () => {
    const error = new HttpErrorResponse({ status: 502, error: { title: 'Down', detail: 'vPIC unreachable' } });
    const states = await firstValueFrom(toLoadState<string[]>(throwError(() => error)).pipe(toArray()));
    expect(states.at(-1)).toEqual({ status: 'error', message: 'vPIC unreachable' } as LoadState<string[]>);
  });
});
