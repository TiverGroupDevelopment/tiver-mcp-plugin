import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

const transactionModeSchema = z
  .enum(["auto", "none"])
  .default("auto")
  .describe(
    "How the snippet should interact with Revit transactions. Use 'auto' to wrap the snippet in a transaction, or 'none' when the called code manages its own transactions."
  );

export function registerSendCodeToRevitTool(server: McpServer) {
  server.tool(
    "send_code_to_revit",
    "Send C# code to Revit for execution. The code will be inserted into a template with access to the Revit Document and parameters. Your code should be written to work within the Execute method of the template.",
    {
      code: z
        .string()
        .describe(
          "The C# code to execute in Revit. This code will be inserted into the Execute method of a template with access to Document and parameters."
        ),
      parameters: z
        .array(z.string())
        .optional()
        .describe(
          "Optional execution parameters that will be passed to your code"
        ),
      transactionMode: transactionModeSchema,
      suppressFailures: z
        .boolean()
        .default(true)
        .describe(
          "Suppress Revit failure dialogs: warnings are deleted and returned in 'warnings', errors roll the transaction back and are returned in 'failures'. Works in both transaction modes. The 'duplicate types' dialog when copying between documents is not a failure; handle it in code via CopyPasteOptions.SetDuplicateTypeNamesHandler."
        ),
    },
    async (args, extra) => {
      const params = {
        code: args.code,
        parameters: args.parameters || [],
        transactionMode: args.transactionMode,
        suppressFailures: args.suppressFailures,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("send_code_to_revit", params);
        });

        return {
          content: [
            {
              type: "text",
              text: `Code execution successful!\nResult: ${JSON.stringify(
                response,
                null,
                2
              )}`,
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Code execution failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
