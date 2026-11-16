import type { JSX } from "@solidjs/web";
import { createUniqueId } from "solid-js";
import { Tooltip, TooltipPanel, TooltipTrigger } from "terracotta/tooltip";

import { Link } from "../router.tsx";
import styles from "./IconControl.module.css";

interface IconControlProps {
  /** Names the control, for screen readers and in its tooltip. */
  readonly label: string;
  /** Completes the name for screen readers, such as the item that a row action acts on. */
  readonly subject?: string;
  /** Shows the icon in the text color, for the main action of a row. */
  readonly main?: boolean;
  readonly children: JSX.Element;
}

interface IconButtonProps extends IconControlProps {
  readonly type?: "button" | "submit";
  readonly onClick?: () => void;
  readonly disabled?: boolean;
}

interface IconLinkProps extends IconControlProps {
  readonly href: string;
  readonly current?: boolean;
  /** Saves the target as a file instead of opening it. */
  readonly download?: boolean;
  /** Opens the target in a new tab, without telling it which page linked to it. */
  readonly newTab?: boolean;
}

/** Renders a button that shows only its icon, with its label in a tooltip. */
export function IconButton(props: IconButtonProps) {
  return (
    <WithTooltip label={props.label}>
      {(anchor) => (
        <TooltipTrigger
          class={styles.control}
          style={anchor}
          data-main={props.main}
          type={props.type ?? "button"}
          disabled={props.disabled}
          onClick={props.onClick}
        >
          <Content {...props} />
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
          data-main={props.main}
          href={props.href}
          download={props.download === true ? "" : undefined}
          target={props.newTab === true ? "_blank" : undefined}
          rel={props.newTab === true ? "noopener noreferrer" : undefined}
          aria-current={props.current === true ? "page" : undefined}
        >
          <Content {...props} />
        </TooltipTrigger>
      )}
    </WithTooltip>
  );
}

function Content(props: IconControlProps) {
  return (
    <>
      {props.children}
      <span class="visually-hidden">
        {props.subject === undefined ? props.label : `${props.label} ${props.subject}`}
      </span>
    </>
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
