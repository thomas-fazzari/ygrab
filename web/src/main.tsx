import "@/app/styles/index.css";

import { render } from "@solidjs/web";

import { App } from "@/app/App.tsx";

const root = document.querySelector("#root");
if (root === null) {
  throw new Error("The page has no #root element");
}

render(() => <App />, root);
