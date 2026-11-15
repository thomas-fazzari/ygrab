// Reads TypeScript annotations from the syntax tree.
// The rules see one file and no type checker, so they judge only what a declaration spells out.

import type { ESTree, SourceCode } from "@oxlint/plugins";

export type FunctionNode =
  | ESTree.ArrowFunctionExpression
  | ESTree.Function
  | ESTree.TSCallSignatureDeclaration
  | ESTree.TSConstructSignatureDeclaration
  | ESTree.TSConstructorType
  | ESTree.TSFunctionType
  | ESTree.TSMethodSignature;

export interface AnnotatedParameter {
  readonly name: string;
  readonly type: ESTree.TSType;
}

export function functionVisitor(check: (node: FunctionNode) => void) {
  return {
    ArrowFunctionExpression: check,
    FunctionDeclaration: check,
    FunctionExpression: check,
    TSCallSignatureDeclaration: check,
    TSConstructSignatureDeclaration: check,
    TSConstructorType: check,
    TSDeclareFunction: check,
    TSEmptyBodyFunctionExpression: check,
    TSFunctionType: check,
    TSMethodSignature: check,
  };
}

function bindingOf(
  parameter: ESTree.ParamPattern,
): ESTree.BindingPattern | ESTree.FormalParameterRest {
  return parameter.type === "TSParameterProperty" ? parameter.parameter : parameter;
}

function annotationOf(
  binding: ESTree.BindingPattern | ESTree.FormalParameterRest,
): ESTree.TSType | undefined {
  if (binding.type === "AssignmentPattern") {
    return annotationOf(binding.left);
  }
  return binding.typeAnnotation?.typeAnnotation;
}

export function annotatedParameters(
  node: FunctionNode,
  sourceCode: SourceCode,
): AnnotatedParameter[] {
  const parameters: AnnotatedParameter[] = [];
  for (const parameter of node.params) {
    const binding = bindingOf(parameter);
    const type = annotationOf(binding);
    if (type === undefined) {
      continue;
    }
    const name = binding.type === "Identifier" ? binding.name : sourceCode.getText(binding);
    parameters.push({ name, type });
  }
  return parameters;
}

export function includesType(
  type: ESTree.TSType,
  isKind: (type: ESTree.TSType) => boolean,
): boolean {
  if (type.type === "TSParenthesizedType") {
    return includesType(type.typeAnnotation, isKind);
  }
  if (type.type === "TSUnionType") {
    return type.types.some((member) => includesType(member, isKind));
  }
  return isKind(type);
}

export function isUnknown(type: ESTree.TSType): boolean {
  return type.type === "TSUnknownKeyword";
}

export function referenceName(type: ESTree.TSType): string | undefined {
  if (type.type !== "TSTypeReference" || type.typeName.type !== "Identifier") {
    return undefined;
  }
  return type.typeName.name;
}

// Generic constraints such as `T extends Record<string, unknown>` describe what a caller may pass.
export function isInTypeParameterConstraint(node: ESTree.Node): boolean {
  let child: ESTree.Node = node;
  for (
    let parent = node.parent;
    parent !== null && parent.type !== "Program";
    parent = parent.parent
  ) {
    if (parent.type === "TSTypeParameter" && parent.constraint === child) {
      return true;
    }
    child = parent;
  }
  return false;
}
