// unplugin-icons ships Solid 1 typings, which read `JSX` from solid-js
declare module "~icons/*" {
  import type { JSX } from "@solidjs/web";

  const component: (props: JSX.SvgSVGAttributes<SVGSVGElement>) => JSX.Element;
  export default component;
}
