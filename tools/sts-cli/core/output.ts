export interface Output {
  printJson(value: unknown): void;
  printText(value: string): void;
}

export class StreamOutput implements Output {
  private readonly stream: NodeJS.WriteStream;

  constructor(stream: NodeJS.WriteStream = process.stdout) {
    this.stream = stream;
  }

  printJson(value: unknown): void {
    this.stream.write(`${JSON.stringify(value, null, 2)}\n`);
  }

  printText(value: string): void {
    this.stream.write(`${value}\n`);
  }
}

export function printJson(value: unknown, stream: NodeJS.WriteStream = process.stdout): void {
  new StreamOutput(stream).printJson(value);
}

export function printText(value: string, stream: NodeJS.WriteStream = process.stdout): void {
  new StreamOutput(stream).printText(value);
}
