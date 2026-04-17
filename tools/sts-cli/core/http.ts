import { HttpError } from "./errors.ts";
import { parseJsonOrText, type JsonObject } from "./json.ts";

export interface RequestOptions {
  method?: "GET" | "POST";
  body?: JsonObject;
}

export class HttpClient {
  readonly baseUrl: string;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  async request(path: string, options: RequestOptions = {}): Promise<unknown> {
    const method = options.method ?? "GET";
    const response = await fetch(`${this.baseUrl}${path}`, {
      method,
      headers: options.body ? { "Content-Type": "application/json" } : undefined,
      body: options.body ? JSON.stringify(options.body) : undefined
    });

    const text = await response.text();
    const parsed = parseJsonOrText(text);

    if (!response.ok) {
      throw new HttpError(response.status, method, path, parsed);
    }

    return parsed;
  }
}
