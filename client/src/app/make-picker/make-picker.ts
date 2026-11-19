import { Component, ElementRef, computed, inject, model, signal, viewChild } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { LoadState, toLoadState } from '../load-state';
import { Make, VehicleApiService } from '../vehicle-api.service';

/**
 * Type-ahead combobox for the ~12k vPIC makes. Searching is server-side, debounced,
 * and stale responses are dropped by switchMap. Follows the WAI-ARIA combobox pattern.
 */
@Component({
  selector: 'app-make-picker',
  templateUrl: './make-picker.html',
  styleUrl: './make-picker.scss',
  host: { '(document:click)': 'onDocumentClick($event)' },
})
export class MakePicker {
  private readonly api = inject(VehicleApiService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly input = viewChild.required<ElementRef<HTMLInputElement>>('input');

  /** Two-way bound selected make: `[(make)]="..."`. */
  readonly make = model<Make | null>(null);

  protected readonly query = signal('');
  protected readonly open = signal(false);
  protected readonly activeIndex = signal(-1);

  protected readonly results = toSignal(
    toObservable(this.query).pipe(
      map((q) => q.trim()),
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((q) =>
        q.length === 0 ? of<LoadState<Make[]>>({ status: 'idle' }) : toLoadState(this.api.searchMakes(q)),
      ),
    ),
    { initialValue: { status: 'idle' } as LoadState<Make[]> },
  );

  protected readonly options = computed(() => {
    const r = this.results();
    return r.status === 'ok' ? r.data : [];
  });

  protected onInput(value: string): void {
    this.query.set(value);
    this.open.set(true);
    this.activeIndex.set(-1);
    if (this.make() && value !== this.make()!.name) {
      this.make.set(null); // typing again clears the previous selection
    }
  }

  protected select(make: Make): void {
    this.make.set(make);
    this.query.set(make.name);
    this.input().nativeElement.value = make.name;
    this.open.set(false);
  }

  protected clear(): void {
    this.make.set(null);
    this.query.set('');
    this.input().nativeElement.value = '';
    this.input().nativeElement.focus();
  }

  protected onKeydown(event: KeyboardEvent): void {
    const count = this.options().length;
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.open.set(true);
        if (count) this.activeIndex.update((i) => (i + 1) % count);
        break;
      case 'ArrowUp':
        event.preventDefault();
        if (count) this.activeIndex.update((i) => (i <= 0 ? count - 1 : i - 1));
        break;
      case 'Enter': {
        const option = this.options()[this.activeIndex()] ?? (count === 1 ? this.options()[0] : undefined);
        if (option) {
          event.preventDefault();
          this.select(option);
        }
        break;
      }
      case 'Escape':
        this.open.set(false);
        break;
    }
  }

  protected onDocumentClick(event: MouseEvent): void {
    if (!this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }
}
