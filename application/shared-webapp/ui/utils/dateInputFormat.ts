// Per-locale input format for our custom DatePicker/DateRangePicker. Default is dd/MM/yyyy
// (day first, slash separator); Danish uses the local dd-MM-yyyy convention with dashes.
const inputFormatMap: Record<string, string> = {
  "en-US": "dd/MM/yyyy",
  "da-DK": "dd-MM-yyyy"
};

const defaultInputFormat = "dd/MM/yyyy";

export function resolveInputFormat(locale: string | undefined): string {
  return inputFormatMap[locale ?? ""] ?? defaultInputFormat;
}
