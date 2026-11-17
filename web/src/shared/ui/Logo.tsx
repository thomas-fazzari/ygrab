import styles from "./Logo.module.css";

const ART = String.raw`_____.___. ________            ___.
\__  |   |/  _____/___________ \_ |__
 /   |   /   \  __\_  __ \__  \ | __ \
 \____   \    \_\  \  | \// __ \| \_\ \
 / ______|\______  /__|  (____  /___  /
 \/              \/           \/    \/`;

export function Logo() {
  return (
    <>
      <h1 class="visually-hidden" tabindex="-1">
        YGrab
      </h1>
      <pre class={styles.logo} aria-hidden="true">
        {ART}
      </pre>
    </>
  );
}
