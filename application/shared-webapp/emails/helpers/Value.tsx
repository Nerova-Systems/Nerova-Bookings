import { getEmailRenderMode } from "./renderMode";

type ValueProps = {
  path: string;
  sample: string | number;
};

// Emits {{path}} in the built artifact and `sample` in the dev preview. The build wraps the raw
// expression in a `<span>` with `dangerouslySetInnerHTML` so React doesn't HTML-escape Handlebars
// helper invocations like `formatCurrency amount currency="USD"`. The plaintext converter strips
// the wrapping span, so the final `.txt` output stays clean.
export function Value({ path, sample }: ValueProps) {
  if (getEmailRenderMode() !== "build") {
    return <>{sample}</>;
  }

  return <span dangerouslySetInnerHTML={{ __html: `{{${path}}}` }} />;
}
