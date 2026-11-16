import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { listScripts, projectsRoot, scriptProjects } from "../utils/projectScripts.js";

export function registerListProjectScriptsTool(server: McpServer) {
  server.tool(
    "list_project_scripts",
    "List saved project scripts: C# snippets for send_code_to_revit stored in projects/<project>/scripts/<name>.cs, with description and parameter names. A project script is the first step before a command: it needs no build and no Revit restart. Check here before writing new code for send_code_to_revit — the task may already be solved. Run one with run_project_script. Without project the scripts of all projects are listed.",
    {
      project: z.string().optional().describe("Project id (folder under projects/). Omit to list the scripts of all projects."),
    },
    async (args) => {
      const projects = scriptProjects(args.project);
      const scripts = listScripts(args.project).map((script) => ({
        project: script.project,
        name: script.name,
        description: script.description,
        parameters: script.parameters,
        transaction: script.transaction,
        path: script.path,
      }));
      const result = {
        root: projectsRoot(),
        projects,
        scripts,
        note:
          projects.length === 0
            ? `Папки проектов не найдены (${projectsRoot()}).`
            : scripts.length === 0
              ? "Скриптов нет. Новый скрипт — файл projects/<проект>/scripts/<имя>.cs с шапкой // description: …, // parameters: …"
              : undefined,
      };
      return { content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }] };
    }
  );
}
