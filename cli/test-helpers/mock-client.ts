import type { RequestClient, RequestOptions } from "../core/client.ts";

interface RecordedRequest {
  path: string;
  options?: RequestOptions;
}

export class MockClient implements RequestClient {
  private readonly responses: Map<string, unknown[]>;
  readonly requests: RecordedRequest[] = [];

  constructor(responses: Record<string, unknown | unknown[]>) {
    this.responses = new Map(
      Object.entries(responses).map(([path, value]) => [path, Array.isArray(value) ? [...value] : [value]])
    );
  }

  async request<T = unknown>(path: string, options?: RequestOptions): Promise<T> {
    this.requests.push({ path, options });

    const queue = this.responses.get(path);
    if (!queue || queue.length === 0) {
      throw new Error(`No mock response configured for ${path}`);
    }

    return queue.shift() as T;
  }
}
