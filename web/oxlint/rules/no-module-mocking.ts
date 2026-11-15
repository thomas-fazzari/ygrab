import { defineRule } from "@oxlint/plugins";

const frameworks = new Set(["vi", "jest"]);
const mockMethods = new Set(["mock", "doMock", "unstable_mockModule"]);

export const noModuleMocking = defineRule({
  meta: {
    type: "problem",
    docs: { description: "Bans Vitest and Jest module mocking." },
    messages: {
      moduleMock:
        "Module mocking replaces code behind the test's back. Pass the dependency in through a real interface, or use a faithful fake.",
    },
  },
  createOnce(context) {
    return {
      CallExpression(node) {
        const callee = node.callee;
        if (
          callee.type === "MemberExpression" &&
          !callee.computed &&
          callee.object.type === "Identifier" &&
          frameworks.has(callee.object.name) &&
          callee.property.type === "Identifier" &&
          mockMethods.has(callee.property.name)
        ) {
          context.report({ node, messageId: "moduleMock" });
        }
      },
    };
  },
});
