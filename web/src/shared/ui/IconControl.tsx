import type { JSX } from "@solidjs/web";
import { createUniqueId } from "solid-js";
import { Tooltip, TooltipPanel, TooltipTrigger } from "terracotta/tooltip";

import { Link } from "../router.tsx";
import styles from "./IconControl.module.css";

interface IconControlProps {
  /** Names the control, for screen readers and in its tooltip. */
  readonly label: string;
  readonly children: JSX.Element;
}

interface IconButtonProps extends IconControlProps {
  readonly onClick: () => void;
  readonly disabled?: boolean;
}

interface IconLinkProps extends IconControlProps {
  readonly href: string;
  readonly current?: boolean;
}

/** Renders a button that shows only its icon, with its label in a tooltip. */
export function IconButton(props: IconButtonProps) {
  return (
    <WithTooltip label={props.label}>
      {(anchor) => (
        <TooltipTrigger
          class={styles.control}
          style={anchor}
          type="button"
          disabled={props.disabled}
          onClick={props.onClick}
        >
          {props.children}
          <span class="visually-hidden">{props.label}</span>
        </TooltipTrigger>
      )}
    </WithTooltip>
  );
}

/** Renders a link that shows only its icon, with its label in a tooltip. */
export function IconLink(props: IconLinkProps) {
  return (
    <WithTooltip label={props.label}>
      {(anchor) => (
        <TooltipTrigger
          as={Link}
          class={styles.control}
          style={anchor}
          href={props.href}
          aria-current={props.current === true ? "page" : undefined}
        >
          {props.children}
          <span class="visually-hidden">{props.label}</span>
        </TooltipTrigger>
      )}
    </WithTooltip>
  );
}

interface WithTooltipProps {
  readonly label: string;
  readonly children: (anchor: JSX.CSSProperties) => JSX.Element;
}

// Ties the tooltip to its control through a CSS anchor of its own
function WithTooltip(props: WithTooltipProps) {
  const anchor = { "--anchor": `--tooltip-${createUniqueId()}` };
  return (
    <Tooltip class={styles.tooltip} defaultOpen={false}>
      {props.children(anchor)}
      <TooltipPanel class={styles.panel} style={anchor}>
        {props.label}
      </TooltipPanel>
    </Tooltip>
  );
}
