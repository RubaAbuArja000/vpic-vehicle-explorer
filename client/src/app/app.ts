import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of, switchMap } from 'rxjs';
import { LoadState, idle, toLoadState } from './load-state';
import { MakePicker } from './make-picker/make-picker';
import { Make, Model, VehicleApiService, VehicleType } from './vehicle-api.service';

const FIRST_YEAR = 1981; // keep in sync with MakesController.MinYear

@Component({
  selector: 'app-root',
  imports: [MakePicker],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly api = inject(VehicleApiService);

  // Newest first; next year's models are usually already published.
  protected readonly years = Array.from(
    { length: new Date().getFullYear() + 1 - FIRST_YEAR + 1 },
    (_, i) => new Date().getFullYear() + 1 - i,
  );

  // --- User selections -------------------------------------------------------
  protected readonly make = signal<Make | null>(null);
  protected readonly year = signal<number | null>(null);
  protected readonly vehicleType = signal<string | null>(null); // null = all types
  protected readonly modelFilter = signal('');
  private readonly retryCount = signal(0);

  // --- Server data (each a LoadState signal; switchMap cancels stale requests) --
  protected readonly types = toSignal(
    toObservable(this.make).pipe(
      switchMap((m) => (m ? toLoadState(this.api.getVehicleTypes(m.id)) : of(idle<VehicleType[]>()))),
    ),
    { initialValue: idle<VehicleType[]>() },
  );

  private readonly modelsQuery = computed(() => {
    const make = this.make();
    const year = this.year();
    this.retryCount(); // dependency so "Try again" re-issues the same query
    return make && year ? { make, year, type: this.vehicleType() } : null;
  });

  protected readonly models = toSignal(
    toObservable(this.modelsQuery).pipe(
      switchMap((q) => (q ? toLoadState(this.api.getModels(q.make.id, q.year, q.type)) : of(idle<Model[]>()))),
    ),
    { initialValue: idle<Model[]>() },
  );

  protected readonly visibleModels = computed(() => {
    const state = this.models();
    if (state.status !== 'ok') return [];
    const term = this.modelFilter().trim().toLowerCase();
    return term ? state.data.filter((m) => m.name.toLowerCase().includes(term)) : state.data;
  });

  // --- Event handlers ----------------------------------------------------------
  protected onMakeChange(make: Make | null): void {
    this.make.set(make);
    this.vehicleType.set(null); // types differ per make
    this.modelFilter.set('');
  }

  protected onYearChange(value: string): void {
    this.year.set(value ? Number(value) : null);
    this.modelFilter.set('');
  }

  protected retry(): void {
    this.retryCount.update((n) => n + 1);
  }
}
