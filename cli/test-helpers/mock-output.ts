import type { Output } from "../core/output.ts";

export class MockOutput implements Output {
  readonly jsonValues: unknown[] = [];
  readonly textValues: string[] = [];

  printJson(value: unknown): void {
    this.jsonValues.push(value);
  }

  printText(value: string): void {
    this.textValues.push(value);
  }
}
