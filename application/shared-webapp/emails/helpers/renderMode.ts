// In "build" mode the helpers emit raw Handlebars expressions like {{path}}; the rendered HTML/text
// becomes the artifact stored at <system>/WebApp/emails/dist/. In "preview" mode (the default) the
// helpers substitute the sample props so the React Email dev server shows realistic content.
export type EmailRenderMode = "build" | "preview";

export function getEmailRenderMode(): EmailRenderMode {
  return process.env.EMAIL_RENDER_MODE === "build" ? "build" : "preview";
}
