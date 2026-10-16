import { Observable } from 'rxjs';

export function streamSse<T>(url: string, body: unknown): Observable<T> {
  return new Observable<T>((subscriber) => {
    const controller = new AbortController();

    (async () => {
      try {
        const res = await fetch(url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
          signal: controller.signal
        });

        if (!res.ok || !res.body) {
          subscriber.error(new Error(`HTTP ${res.status}`));
          return;
        }

        if (res.headers.get('Content-Type')?.includes('application/json')) {
          subscriber.error(await res.json());
          return;
        }

        const reader = res.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
          const { value, done } = await reader.read();
          if (done) break;
          buffer += decoder.decode(value, { stream: true });

          let idx: number;
          while ((idx = buffer.indexOf('\n\n')) >= 0) {
            const chunk = buffer.slice(0, idx).trim();
            buffer = buffer.slice(idx + 2);
            if (chunk.startsWith('data:')) {
              const json = chunk.slice(5).trim();
              if (json) {
                subscriber.next(JSON.parse(json) as T);
              }
            }
          }
        }

        subscriber.complete();
      } catch (err) {
        if (!controller.signal.aborted) {
          subscriber.error(err);
        }
      }
    })();

    return () => controller.abort();
  });
}
