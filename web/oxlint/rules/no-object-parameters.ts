import { defineRule } from "@oxlint/plugins";

import { annotatedParameters, functionVisitor, includesType } from "../types.ts";

export const noObjectParameters = defineRule({
  meta: {
    type: "problem",
    docs: { description: "Bans the broad `object` type on function parameters." },
    messages: {
      objectParameter:
        "Parameter `{{parameter}}` accepts any object. Accept the named type that the caller has.",
    },
  },
  createOnce(context) {
    return functionVisitor((node) => {
      for (const { name, type } of annotatedParameters(node, context.sourceCode)) {
        if (includesType(type, (member) => member.type === "TSObjectKeyword")) {
          context.report({ node: type, messageId: "objectParameter", data: { parameter: name } });
        }
      }
    });
  },
});
