export class CliError extends Error {
  exitCode: number;

  constructor(message: string, exitCode = 1) {
    super(message);
    this.exitCode = exitCode;
  }
}

export class HttpError extends Error {
  status: number;
  method: string;
  path: string;
  body: unknown;

  constructor(status: number, method: string, path: string, body: unknown) {
    super(`HTTP ${status} for ${method} ${path}`);
    this.status = status;
    this.method = method;
    this.path = path;
    this.body = body;
  }
}
