import { defineRule } from "@oxlint/plugins";
import type { ESTree } from "@oxlint/plugins";

const iteratorSources = new Set(["values", "keys", "entries"]);

function methodCall(
  node: ESTree.Expression,
): { name: string; receiver: ESTree.Expression } | undefined {
  if (
    node.type !== "CallExpression" ||
    node.callee.type !== "MemberExpression" ||
    node.callee.computed
  ) {
    return undefined;
  }
  if (node.callee.property.type !== "Identifier" || node.callee.object.type === "Super") {
    return undefined;
  }
  return { name: node.callee.property.name, receiver: node.callee.object };
}

// Iterator helpers such as `values().filter().map()` are lazy and allowed.
function isIterator(node: ESTree.Expression): boolean {
  const call = methodCall(node);
  if (call === undefined) {
    return false;
  }
  if (iteratorSources.has(call.name)) {
    return true;
  }
  return (call.name === "filter" || call.name === "map") && isIterator(call.receiver);
}

export const noArrayFilterMap = defineRule({
  meta: {
    type: "suggestion",
    docs: { description: "Bans adjacent array filter and map passes." },
    messages: {
      filterMap:
        "This chain walks the array twice and builds an array between the passes. Use one `flatMap`, one loop, or iterator helpers.",
    },
  },
  createOnce(context) {
    return {
      CallExpression(node) {
        const outer = methodCall(node);
        if (outer === undefined || (outer.name !== "map" && outer.name !== "filter")) {
          return;
        }
        const inner = methodCall(outer.receiver);
        const other = outer.name === "map" ? "filter" : "map";
        if (inner?.name === other && !isIterator(inner.receiver)) {
          context.report({ node, messageId: "filterMap" });
        }
      },
    };
  },
});
