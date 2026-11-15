import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerDismissRevitDialogTool(server: McpServer) {
  registerFamilyTool(
    server,
    "dismiss_revit_dialog",
    "Read or close a modal Revit dialog (error from another add-in or updater, 'Save changes?', warnings) that blocks Revit — the usual reason a tool answers 'Revit did not respond in 100 s'. Works while Revit is blocked. By default presses Cancel (rolls back the failed operation); if there is no Cancel, Close or OK. Check the model state afterwards: a blocked command may complete once the dialog is closed.",
    {
      listOnly: z.boolean().optional().describe("Only list open dialogs with their texts and buttons, press nothing"),
      button: z
        .string()
        .optional()
        .describe("Button text to press, e.g. 'Отмена', 'Cancel', 'OK', 'Удалить элементы' (default: Cancel / Close / OK)"),
      handle: z.number().int().optional().describe("Dialog handle from listOnly, when several dialogs are open"),
      all: z.boolean().optional().describe("Keep closing dialogs while they appear (up to 10)"),
    }
  );
}
