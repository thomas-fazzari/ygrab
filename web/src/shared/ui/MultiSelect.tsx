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

import buttonStyles from "./Button.module.css";
import choiceStyles from "./Choices.module.css";
import { widestOf } from "./Choices.tsx";
import { createListboxDisclosure } from "./listboxDisclosure.ts";
import styles from "./MultiSelect.module.css";

interface MultiSelectProps<TValue extends string> {
  /** Names the list for screen readers, and the summary when all or no choice is checked. */
  readonly name: string;
  readonly choices: readonly TValue[];
  readonly labels: Readonly<Record<TValue, string>>;
  readonly value: readonly TValue[];
  readonly onChange: (value: TValue[]) => void;
}

const allOf = (name: string) => `All ${name.toLowerCase()}`;
const noneOf = (name: string) => `No ${name.toLowerCase()}`;

function summaryOf<TValue extends string>(props: MultiSelectProps<TValue>) {
  const first = props.choices.find((choice) => props.value.includes(choice));
  if (first === undefined) {
    return noneOf(props.name);
  }
  if (props.value.length === props.choices.length) {
    return allOf(props.name);
  }
  const others = props.value.length - 1;
  return others === 0 ? props.labels[first] : `${props.labels[first]} +${others}`;
}

// The summaries at their longest, when every choice but one is checked
function summariesOf<TValue extends string>(props: MultiSelectProps<TValue>) {
  const most = props.choices.length - 2;
  return [
    allOf(props.name),
    noneOf(props.name),
    ...props.choices.map((choice) => `${props.labels[choice]} +${most}`),
  ];
}

/**
 * Renders a button that opens a checklist of the choices.
 *
 * The button shows `All <name>` when every choice is checked and `No <name>` when none is.
 * Otherwise it shows the first checked choice and how many others are checked, such as `Info +2`.
 */
export function MultiSelect<TValue extends string>(props: MultiSelectProps<TValue>) {
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
      <ListboxLabel class="visually-hidden">{props.name}</ListboxLabel>
      <ListboxButton class={[buttonStyles.button, choiceStyles.trigger]} style={anchor}>
        <span class={choiceStyles.summary} style={widestOf(summariesOf(props))}>
          {summaryOf(props)}
        </span>
        <ChevronIcon />
      </ListboxButton>
      <ListboxOptions class={choiceStyles.options} style={anchor}>
        <For each={props.choices}>
          {(choice) => (
            <ListboxOption class={[choiceStyles.option, styles.option]} value={choice}>
              <CheckIcon class={styles.check} />
              {props.labels[choice]}
            </ListboxOption>
          )}
        </For>
      </ListboxOptions>
    </Listbox>
  );
}
