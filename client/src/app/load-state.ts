import { HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, map, of, startWith } from 'rxjs';

/** One shape for every async request the UI renders: idle -> loading -> ok | error. */
export type LoadState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ok'; data: T }
  | { status: 'error'; message: string };

export const idle = <T>(): LoadState<T> => ({ status: 'idle' });

/** Wraps a request so it emits `loading` first and never errors out of the stream. */
export function toLoadState<T>(source: Observable<T>): Observable<LoadState<T>> {
  return source.pipe(
    map((data): LoadState<T> => ({ status: 'ok', data })),
    startWith<LoadState<T>>({ status: 'loading' }),
    catchError((err: unknown) => of<LoadState<T>>({ status: 'error', message: describeError(err) })),
  );
}

export function describeError(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) return 'Could not reach the server. Check your connection.';
    // Our API returns RFC 7807 problem details; prefer its human-readable fields.
    const problem = err.error as { detail?: string; title?: string } | null;
    return problem?.detail ?? problem?.title ?? `Request failed (${err.status}).`;
  }
  return 'Something went wrong.';
}
