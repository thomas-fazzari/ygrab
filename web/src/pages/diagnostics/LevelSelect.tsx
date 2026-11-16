import { createUniqueId, For } from "solid-js";
import {
  Listbox,
  ListboxButton,
  ListboxLabel,
  ListboxOption,
  ListboxOptions,
} from "terracotta/listbox";
import ChevronIcon from "~icons/solar/alt-arrow-down-linear";
import CheckIcon from "~icons/solar/check-linear";

import buttonStyles from "@/shared/ui/Button.module.css";
import choiceStyles from "@/shared/ui/Choices.module.css";
import { createListboxDisclosure } from "@/shared/ui/listboxDisclosure.ts";
import styles from "./LevelSelect.module.css";
import { LEVEL_FILTER_LABELS, LEVEL_FILTERS } from "./logText.ts";
import type { LevelFilter } from "./logText.ts";

interface LevelSelectProps {
  readonly value: readonly LevelFilter[];
  readonly onChange: (value: LevelFilter[]) => void;
}

// First checked level, then how many others are checked (e.g. `Info +2`)
function summaryOf(value: readonly LevelFilter[]) {
  const first = LEVEL_FILTERS.find((level) => value.includes(level));
  if (first === undefined) {
    return "No level";
  }
  const others = value.length - 1;
  return others === 0 ? LEVEL_FILTER_LABELS[first] : `${LEVEL_FILTER_LABELS[first]} +${others}`;
}

/** Renders a button that opens a checklist of the log levels to show. */
export function LevelSelect(props: LevelSelectProps) {
  const anchor = { "--anchor": `--select-${createUniqueId()}` };
  const disclosure = createListboxDisclosure();
  return (
    <Listbox
      ref={disclosure.ref}
      class={choiceStyles.picker}
      multiple
      toggleable
      value={[...props.value]}
      isOpen={disclosure.isOpen()}
      onDisclosureChange={disclosure.onChange}
      onSelectChange={props.onChange}
    >
      <ListboxLabel class="visually-hidden">Levels</ListboxLabel>
      <ListboxButton class={[buttonStyles.button, choiceStyles.trigger]} style={anchor}>
        {summaryOf(props.value)}
        <ChevronIcon />
      </ListboxButton>
      <ListboxOptions class={choiceStyles.options} style={anchor}>
        <For each={LEVEL_FILTERS}>
          {(level) => (
            <ListboxOption class={[choiceStyles.option, styles.option]} value={level}>
              <CheckIcon class={styles.check} />
              {LEVEL_FILTER_LABELS[level]}
            </ListboxOption>
          )}
        </For>
      </ListboxOptions>
    </Listbox>
  );
}
