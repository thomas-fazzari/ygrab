import { defineRule } from "@oxlint/plugins";
import type { ESTree } from "@oxlint/plugins";

import { includesType, isInTypeParameterConstraint, referenceName } from "../types.ts";

function isLoose(type: ESTree.TSType): boolean {
  if (type.type === "TSTypeLiteral") {
    return type.members.length === 0;
  }
  return (
    type.type === "TSUnknownKeyword" ||
    type.type === "TSAnyKeyword" ||
    type.type === "TSObjectKeyword"
  );
}

export const noUnsafeDictionaryType = defineRule({
  meta: {
    type: "problem",
    docs: { description: "Bans dictionaries whose values are unknown, any, object or {}." },
    messages: {
      unsafeDictionary:
        "This dictionary says nothing about its values. Use the type that the values really have, and type data where it enters the app.",
    },
  },
  createOnce(context) {
    const check = (node: ESTree.Node, value: ESTree.TSType | undefined) => {
      if (
        value !== undefined &&
        includesType(value, isLoose) &&
        !isInTypeParameterConstraint(node)
      ) {
        context.report({ node, messageId: "unsafeDictionary" });
      }
    };
    return {
      TSTypeReference(node) {
        if (referenceName(node) === "Record") {
          check(node, node.typeArguments?.params[1]);
        }
      },
      TSIndexSignature(node) {
        check(node, node.typeAnnotation.typeAnnotation);
      },
    };
  },
});
