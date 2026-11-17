import { createUniqueId, For } from "solid-js";
import {
  Listbox,
  ListboxButton,
  ListboxLabel,
  ListboxOption,
  ListboxOptions,
} from "terracotta/listbox";
import ChevronIcon from "~icons/solar/alt-arrow-down-linear";

import buttonStyles from "./Button.module.css";
import styles from "./Choices.module.css";
import { createDisclosure } from "./disclosure.ts";

interface ChoicesProps<TValue extends string> {
  /** Names the group for screen readers and its radios for the form. */
  readonly name: string;
  readonly choices: readonly TValue[];
  readonly labels: Readonly<Record<TValue, string>>;
  readonly value: TValue;
  readonly onChange: (value: TValue) => void;
  readonly class?: string | undefined;
}

/**
 * Sizes a summary to the longest text it can show, so its button keeps its width when the choice changes.
 *
 * The width counts characters, which holds for the monospace font of the app.
 */
export const widestOf = (texts: readonly string[]) => ({
  "--summary-width": `${Math.max(0, ...texts.map((text) => text.length))}ch`,
});

/** Renders one radio per choice, each drawn as a button. */
export function ChoiceChips<TValue extends string>(props: ChoicesProps<TValue>) {
  return (
    <div class={[styles.chips, props.class]} role="radiogroup" aria-label={props.name}>
      <For each={props.choices}>
        {(choice) => (
          <label class={[buttonStyles.button, styles.chip]}>
            <input
              class="visually-hidden"
              type="radio"
              name={props.name}
              checked={props.value === choice}
              onChange={() => {
                props.onChange(choice);
              }}
            />
            {props.labels[choice]}
          </label>
        )}
      </For>
    </div>
  );
}

/** Renders the choices as a button that opens a list, for narrow screens. */
export function ChoiceSelect<TValue extends string>(props: ChoicesProps<TValue>) {
  const anchor = { "--anchor": `--select-${createUniqueId()}` };
  const choose = (choice?: TValue) => {
    if (choice !== undefined) {
      props.onChange(choice);
    }
  };
  const disclosure = createDisclosure();
  return (
    <Listbox
      ref={disclosure.ref}
      class={[styles.picker, props.class]}
      value={props.value}
      isOpen={disclosure.isOpen()}
      onDisclosureChange={disclosure.onChange}
      onSelectChange={choose}
    >
      <ListboxLabel class="visually-hidden">{props.name}</ListboxLabel>
      <ListboxButton class={[buttonStyles.button, styles.trigger]} style={anchor}>
        <span
          class={styles.summary}
          style={widestOf(props.choices.map((choice) => props.labels[choice]))}
        >
          {props.labels[props.value]}
        </span>
        <ChevronIcon />
      </ListboxButton>
      <ListboxOptions class={styles.options} style={anchor}>
        <For each={props.choices}>
          {(choice) => (
            <ListboxOption class={styles.option} value={choice}>
              {props.labels[choice]}
            </ListboxOption>
          )}
        </For>
      </ListboxOptions>
    </Listbox>
  );
}
