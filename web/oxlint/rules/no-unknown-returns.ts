import { defineRule } from "@oxlint/plugins";
import type { ESTree } from "@oxlint/plugins";

import { functionVisitor, includesType, isUnknown, referenceName } from "../types.ts";

function isUnknownOrPromiseOfUnknown(type: ESTree.TSType): boolean {
  if (isUnknown(type)) {
    return true;
  }
  const name = referenceName(type);
  const value = type.type === "TSTypeReference" ? type.typeArguments?.params[0] : undefined;
  return (
    (name === "Promise" || name === "PromiseLike") &&
    value !== undefined &&
    includesType(value, isUnknown)
  );
}

export const noUnknownReturns = defineRule({
  meta: {
    type: "problem",
    docs: { description: "Bans functions that return unknown or a promise of unknown." },
    messages: {
      unknownReturn:
        "This function hands its caller a value that nobody has typed. Type the value here, at its source, and return that type.",
    },
  },
  createOnce(context) {
    return functionVisitor((node) => {
      const type = node.returnType?.typeAnnotation;
      if (type !== undefined && includesType(type, isUnknownOrPromiseOfUnknown)) {
        context.report({ node: type, messageId: "unknownReturn" });
      }
    });
  },
});
