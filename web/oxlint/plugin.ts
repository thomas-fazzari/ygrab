// YGrab's Oxlint rules.

import { definePlugin } from "@oxlint/plugins";

import { noArrayFilterMap } from "./rules/no-array-filter-map.ts";
import { noConditionalEmptyObjectSpread } from "./rules/no-conditional-empty-object-spread.ts";
import { noModuleMocking } from "./rules/no-module-mocking.ts";
import { noObjectParameters } from "./rules/no-object-parameters.ts";
import { noRecordGuard } from "./rules/no-record-guard.ts";
import { noRuntimeTypeof } from "./rules/no-runtime-typeof.ts";
import { noUnknownParameters } from "./rules/no-unknown-parameters.ts";
import { noUnknownReturns } from "./rules/no-unknown-returns.ts";
import { noUnknownTypeAliases } from "./rules/no-unknown-type-aliases.ts";
import { noUnsafeDictionaryType } from "./rules/no-unsafe-dictionary-type.ts";

export default definePlugin({
  meta: { name: "ygrab" },
  rules: {
    "no-array-filter-map": noArrayFilterMap,
    "no-conditional-empty-object-spread": noConditionalEmptyObjectSpread,
    "no-module-mocking": noModuleMocking,
    "no-object-parameters": noObjectParameters,
    "no-record-guard": noRecordGuard,
    "no-runtime-typeof": noRuntimeTypeof,
    "no-unknown-parameters": noUnknownParameters,
    "no-unknown-returns": noUnknownReturns,
    "no-unknown-type-aliases": noUnknownTypeAliases,
    "no-unsafe-dictionary-type": noUnsafeDictionaryType,
  },
});
