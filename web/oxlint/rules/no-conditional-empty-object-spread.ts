import { defineRule } from "@oxlint/plugins";
import type { ESTree } from "@oxlint/plugins";

function isEmptyObject(node: ESTree.Expression): boolean {
  return node.type === "ObjectExpression" && node.properties.length === 0;
}

// `cond && { field }` spreads `false` when the condition fails, which leaves the field out too.
function leavesFieldOut(node: ESTree.Expression): boolean {
  if (node.type === "ConditionalExpression") {
    return isEmptyObject(node.consequent) || isEmptyObject(node.alternate);
  }
  return (
    node.type === "LogicalExpression" &&
    node.operator === "&&" &&
    node.right.type === "ObjectExpression"
  );
}

export const noConditionalEmptyObjectSpread = defineRule({
  meta: {
    type: "suggestion",
    docs: {
      description:
        "Bans spreading `cond ? { field } : {}` or `cond && { field }` to leave a field out.",
    },
    messages: {
      emptySpread:
        "This spread hides a missing field behind an empty object. Build the object step by step, or give the field a value that says it is absent.",
    },
  },
  createOnce(context) {
    const check = (node: ESTree.Node, spread: ESTree.Expression) => {
      let argument = spread;
      while (argument.type === "ParenthesizedExpression") {
        argument = argument.expression;
      }
      if (leavesFieldOut(argument)) {
        context.report({ node, messageId: "emptySpread" });
      }
    };
    return {
      SpreadElement(node) {
        if (node.parent.type === "ObjectExpression") {
          check(node, node.argument);
        }
      },
      // JSX props spread the same way, as in `<Header {...(cond ? {} : { subtitle })} />`.
      JSXSpreadAttribute(node) {
        check(node, node.argument);
      },
    };
  },
});
