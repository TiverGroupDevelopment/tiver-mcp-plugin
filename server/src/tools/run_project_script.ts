import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { findScript, readScriptBody } from "../utils/projectScripts.js";

export function registerRunProjectScriptTool(server: McpServer) {
  server.tool(
    "run_project_script",
    "Run a saved project script (projects/<project>/scripts/<name>.cs) in Revit through send_code_to_revit: the file is the body of Execute(Document document, object[] parameters). Parameters are passed in the order listed in the script header (see list_project_scripts). Transaction mode and extra usings come from the header. Needs an open document in Revit. Without project the script is looked up by name in all projects; if several projects have a script with this name, pass project.",
    {
      name: z.string().describe("Script name without .cs (see list_project_scripts)"),
      parameters: z.array(z.string()).optional().describe("Values in the order of the script's parameters header"),
      project: z.string().optional().describe("Project id (folder under projects/). Needed only when several projects have a script with this name."),
      suppressFailures: z.boolean().optional().describe("Suppress Revit failure dialogs (default true), as in send_code_to_revit"),
    },
    async (args) => {
      try {
        const script = findScript(args.name, args.project);
        const values = args.parameters ?? [];
        if (values.length < script.parameters.length) {
          throw new Error(
            `Скрипт '${script.name}' ждёт параметры: ${script.parameters.join(", ")}. Передано ${values.length} из ${script.parameters.length}.`
          );
        }
        const body = readScriptBody(script);
        const response = await withRevitConnection((client) =>
          client.sendCommand("send_code_to_revit", {
            code: body.code,
            parameters: values,
            transactionMode: script.transaction,
            suppressFailures: args.suppressFailures ?? true,
            classes: body.classes,
            usings: script.usings,
          })
        );
        const result = { script: { project: script.project, name: script.name, path: script.path }, result: response };
        return { content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }] };
      } catch (error) {
        return {
          content: [{ type: "text" as const, text: `run_project_script failed: ${error instanceof Error ? error.message : String(error)}` }],
          isError: true,
        };
      }
    }
  );
}
