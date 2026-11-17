import SearchIcon from "~icons/solar/magnifer-linear";

import { Field } from "./Field.tsx";
import styles from "./SearchField.module.css";

interface SearchFieldProps {
  readonly label: string;
  readonly value: string;
  readonly onInput: (value: string) => void;
}

export function SearchField(props: SearchFieldProps) {
  return (
    <div class={styles.search}>
      <Field small>
        <SearchIcon />
        <input
          type="text"
          aria-label={props.label}
          placeholder={props.label.toLowerCase()}
          value={props.value}
          onInput={(event) => {
            props.onInput(event.currentTarget.value);
          }}
        />
      </Field>
    </div>
  );
}
