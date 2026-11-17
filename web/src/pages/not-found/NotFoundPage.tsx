import { Link } from "@/shared/router.tsx";
import { HOME_ROUTE } from "@/shared/session.ts";
import { PageTitle } from "@/shared/ui/PageTitle.tsx";
import styles from "./NotFoundPage.module.css";

export function NotFoundPage() {
  return (
    <>
      <PageTitle>Page not found :(</PageTitle>
      <p class={styles.text}>
        <Link href={HOME_ROUTE}>Back to the home page</Link>
      </p>
    </>
  );
}
