import { defineRule } from "@oxlint/plugins";

import { includesType, isUnknown } from "../types.ts";

export const noUnknownTypeAliases = defineRule({
  meta: {
    type: "problem",
    docs: { description: "Bans type aliases of unknown." },
    messages: {
      unknownAlias:
        "This alias names a value that nobody has typed. Give the value its real type instead.",
    },
  },
  createOnce(context) {
    return {
      TSTypeAliasDeclaration(node) {
        if (includesType(node.typeAnnotation, isUnknown)) {
          context.report({ node: node.typeAnnotation, messageId: "unknownAlias" });
        }
      },
    };
  },
});
