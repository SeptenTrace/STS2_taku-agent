import type {
  ActionSurfaceResponse,
  ContextResponse,
  MenuResponse,
  PingResponse
} from "../api-types.ts";
import type { RequestClient } from "../core/client.ts";
import { buildRoomSummary, type RoomSummaryResult } from "./combo.ts";

export interface DoctorCheck {
  name: string;
  ok: boolean;
  detail?: unknown;
}

export interface DoctorResult {
  ok: boolean;
  context?: ContextResponse;
  checks: DoctorCheck[];
}

export async function runDoctor(client: RequestClient): Promise<DoctorResult> {
  const checks: DoctorCheck[] = [];
  let context: ContextResponse | undefined;
  let actions: ActionSurfaceResponse | undefined;

  try {
    const ping = await client.request<PingResponse>("/");
    checks.push({
      name: "ping",
      ok: ping.status === "ok",
      detail: {
        status: ping.status,
        port: ping.port
      }
    });
  } catch (error) {
    checks.push({
      name: "ping",
      ok: false,
      detail: formatErrorDetail(error)
    });
  }

  try {
    context = await client.request<ContextResponse>("/api/v1/context");
    checks.push({
      name: "context",
      ok: true,
      detail: {
        stateType: context.stateType,
        isStable: context.isStable,
        isTransitioning: context.isTransitioning
      }
    });
  } catch (error) {
    checks.push({
      name: "context",
      ok: false,
      detail: formatErrorDetail(error)
    });
  }

  try {
    actions = await client.request<ActionSurfaceResponse>("/api/v1/actions");
    checks.push({
      name: "actions",
      ok: true,
      detail: {
        stateType: actions.stateType,
        actionCount: actions.actions.length
      }
    });
  } catch (error) {
    checks.push({
      name: "actions",
      ok: false,
      detail: formatErrorDetail(error)
    });
  }

  if (context && actions) {
    checks.push({
      name: "action_state_sync",
      ok: context.stateType === actions.stateType,
      detail: {
        contextStateType: context.stateType,
        actionStateType: actions.stateType
      }
    });
  }

  if (context?.stateType === "menu") {
    await addMenuDoctorChecks(client, checks, actions);
  } else if (context) {
    await addRoomDoctorChecks(client, checks);
  }

  return {
    ok: checks.every((check) => check.ok),
    context,
    checks
  };
}

async function addMenuDoctorChecks(client: RequestClient, checks: DoctorCheck[], actions: ActionSurfaceResponse | undefined): Promise<void> {
  try {
    const menu = await client.request<MenuResponse>("/api/v1/menu");
    checks.push({
      name: "menu",
      ok: true,
      detail: {
        canContinue: menu.canContinue,
        hasContinueRun: menu.hasContinueRun
      }
    });

    if (menu.canContinue) {
      checks.push({
        name: "menu_continue_action",
        ok: actions?.actions.some((action) => action.actionType === "continue_game") === true,
        detail: {
          canContinue: menu.canContinue,
          exposedActionTypes: actions?.actions.map((action) => action.actionType) ?? []
        }
      });
    }
  } catch (error) {
    checks.push({
      name: "menu",
      ok: false,
      detail: formatErrorDetail(error)
    });
  }
}

async function addRoomDoctorChecks(client: RequestClient, checks: DoctorCheck[]): Promise<void> {
  try {
    const roomSummary = await buildRoomSummary(client);
    checks.push({
      name: "room_summary",
      ok: true,
      detail: summarizeRoom(roomSummary)
    });
  } catch (error) {
    checks.push({
      name: "room_summary",
      ok: false,
      detail: formatErrorDetail(error)
    });
  }
}

function summarizeRoom(roomSummary: RoomSummaryResult): Record<string, unknown> {
  return {
    stateType: roomSummary.context.stateType,
    playerSummaryAvailable: roomSummary.playerSummary !== undefined,
    actionCount: roomSummary.actions.actions.length,
    stateDataKind: roomSummary.stateData?.kind
  };
}

function formatErrorDetail(error: unknown): unknown {
  if (error instanceof Error) {
    return {
      name: error.name,
      message: error.message
    };
  }

  return String(error);
}
