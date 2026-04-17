import { HttpClient } from '@angular/common/http';
import { computed, Injectable, Signal, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, Observable, of, shareReplay, Subject, switchMap, take } from 'rxjs';
import { Claim } from './claim';

export type { Claim } from './claim';
export type Session = Array<Claim>;

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private readonly http = inject(HttpClient);
  private readonly _refresh$ = new Subject<void>();

  // Emits null on error, Array<Claim> on success (even empty).
  // Nothing emits until _refresh$.next() is called — no I/O at construction time.
  private readonly _fetchResult$ = this._refresh$.pipe(
    switchMap(() =>
      this.http.get<Array<Claim>>('bff/user').pipe(
        catchError(() => of(null))
      )
    ),
    shareReplay({ bufferSize: 1, refCount: true })
  );

  // Null = not yet loaded or unauthenticated; Array<Claim> = authenticated session.
  private readonly _fetchResult = toSignal(this._fetchResult$, {
    initialValue: null as Array<Claim> | null
  });

  public readonly isAuthenticated: Signal<boolean> = computed(() => this._fetchResult() !== null);
  public readonly isAnonymous: Signal<boolean> = computed(() => this._fetchResult() === null);
  public readonly session: Signal<Session> = computed(() => this._fetchResult() ?? []);
  public readonly username: Signal<string | null> = computed(
    () => this._fetchResult()?.find(x => x.type === 'name')?.value ?? null
  );
  public readonly logoutUrl: Signal<string | null> = computed(() => {
    const s = this._fetchResult();
    if (!s) return null;
    return s.find(x => x.type === 'bff:logout_url')?.value ?? null;
  });

  public readonly silentLoginUrl: string = '/bff/silent-login';
  public readonly loginUrl: string = '/bff/login';

  /** Called by provideAppInitializer — triggers the first fetch and returns an
   *  Observable that completes once the session response is received. */
  public initialize(): Observable<Session> {
    this._refresh$.next();
    return this._fetchResult$.pipe(
      map(s => s ?? []),
      take(1)
    );
  }

  /** Re-fetches the session and updates all signals. Called after silent login. */
  public refresh(): void {
    this._refresh$.next();
  }
}
