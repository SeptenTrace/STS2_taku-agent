export function printJson(value: unknown, stream: NodeJS.WriteStream = process.stdout): void {
  stream.write(`${JSON.stringify(value, null, 2)}\n`);
}

export function printText(value: string, stream: NodeJS.WriteStream = process.stdout): void {
  stream.write(`${value}\n`);
}
