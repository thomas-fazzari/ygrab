import { defineRule } from "@oxlint/plugins";
import type { ESTree } from "@oxlint/plugins";

import { referenceName } from "../types.ts";

const slopGuardNames = new Set(["isRecord", "isObject", "isPlainObject"]);

function isObjectType(type: ESTree.TSType | undefined): boolean {
  if (type?.type === "TSObjectKeyword" || type?.type === "TSTypeLiteral") {
    return true;
  }
  return type !== undefined && referenceName(type) === "Record";
}

export const noRecordGuard = defineRule({
  meta: {
    type: "problem",
    docs: { description: "Bans type guards that narrow unknown values to objects." },
    messages: {
      recordGuard:
        "Do not narrow unknown values to objects. Data that needs this guard has no type where it enters the app. Fix that first: parse the API data with a valibot schema, or store a flat format such as URLSearchParams. Then delete this guard.",
    },
  },
  createOnce(context) {
    const checkName = (node: ESTree.Node | null) => {
      if (node?.type === "Identifier" && slopGuardNames.has(node.name)) {
        context.report({ node, messageId: "recordGuard" });
      }
    };
    return {
      FunctionDeclaration: (node) => checkName(node.id),
      VariableDeclarator: (node) => checkName(node.id),
      TSTypePredicate(node) {
        if (isObjectType(node.typeAnnotation?.typeAnnotation)) {
          context.report({ node, messageId: "recordGuard" });
        }
      },
    };
  },
});
