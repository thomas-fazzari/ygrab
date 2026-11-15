import { defineRule } from "@oxlint/plugins";

import { annotatedParameters, functionVisitor, includesType, isUnknown } from "../types.ts";

// A caught error has no type, so a parameter named `cause` that carries one may stay unknown.
export const noUnknownParameters = defineRule({
  meta: {
    type: "problem",
    docs: { description: "Bans unknown function parameters." },
    messages: {
      unknownParameter:
        "Parameter `{{parameter}}` takes a value that nobody has typed. Type the value where it enters the app, then accept that type here. A type guard on unknown only moves the problem.",
    },
  },
  createOnce(context) {
    return functionVisitor((node) => {
      for (const { name, type } of annotatedParameters(node, context.sourceCode)) {
        if (name === "cause" || !includesType(type, isUnknown)) {
          continue;
        }
        context.report({ node: type, messageId: "unknownParameter", data: { parameter: name } });
      }
    });
  },
});
