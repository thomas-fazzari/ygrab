import { defineRule } from "@oxlint/plugins";
import type { ESTree } from "@oxlint/plugins";

// `typeof window === "undefined"` asks whether a binding exists, not what a value is.
function isExistenceProbe(node: ESTree.UnaryExpression): boolean {
  const parent = node.parent;
  if (parent.type !== "BinaryExpression" || !["===", "!==", "==", "!="].includes(parent.operator)) {
    return false;
  }
  const other = parent.left === node ? parent.right : parent.left;
  return other.type === "Literal" && other.value === "undefined";
}

export const noRuntimeTypeof = defineRule({
  meta: {
    type: "problem",
    docs: { description: "Bans `typeof` checks on values." },
    messages: {
      runtimeTypeof:
        "This `typeof` check guesses what a value is. Type the value where it enters the app: read text as strings, look known values up in their list, and compare typed values with null.",
    },
  },
  createOnce(context) {
    return {
      UnaryExpression(node) {
        if (node.operator === "typeof" && !isExistenceProbe(node)) {
          context.report({ node, messageId: "runtimeTypeof" });
        }
      },
    };
  },
});
