export interface RequestClient {
  request<T = unknown>(path: string, options?: RequestOptions): Promise<T>;
}

export interface RequestOptions {
  method?: "GET" | "POST";
  body?: Record<string, unknown>;
}
