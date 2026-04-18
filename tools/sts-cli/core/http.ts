import { HttpError } from "./errors.ts";
import { parseJsonOrText } from "./json.ts";
import type { RequestClient, RequestOptions } from "./client.ts";

export class HttpClient implements RequestClient {
  readonly baseUrl: string;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  async request<T = unknown>(path: string, options: RequestOptions = {}): Promise<T> {
    const method = options.method ?? "GET";
    const response = await fetch(`${this.baseUrl}${path}`, {
      method,
      headers: {
        ...(options.body ? { "Content-Type": "application/json" } : {}),
        ...(options.headers ?? {})
      },
      body: options.body ? JSON.stringify(options.body) : undefined
    });

    const text = await response.text();
    const parsed = parseJsonOrText(text);

    if (!response.ok) {
      throw new HttpError(response.status, method, path, parsed);
    }

    return parsed as T;
  }
}
