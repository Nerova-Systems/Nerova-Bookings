const WEEKDAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
const MONTHS_SHORT = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const MONTHS_LONG = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December"
];

export function formatDayGroup(date: Date) {
  return `${WEEKDAYS[date.getDay()]} ${twoDigits(date.getDate())} ${MONTHS_LONG[date.getMonth()]}`;
}

export function formatShortDate(date: Date) {
  return `${twoDigits(date.getDate())} ${MONTHS_SHORT[date.getMonth()]}`;
}

export function formatWeekday(date: Date) {
  return WEEKDAYS[date.getDay()];
}

export function formatDayNumber(date: Date) {
  return twoDigits(date.getDate());
}

export function formatTime(date: Date) {
  return `${twoDigits(date.getHours())}:${twoDigits(date.getMinutes())}`;
}

export function formatWholeNumber(value: number) {
  return String(value).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
}

export function businessDateKey(date: Date, timeZone: string) {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit"
  }).formatToParts(date);
  const values = Object.fromEntries(parts.map((part) => [part.type, part.value]));
  return `${values.year}-${values.month}-${values.day}`;
}

export function isTodayInTimeZone(date: Date, timeZone: string, now = new Date()) {
  return businessDateKey(date, timeZone) === businessDateKey(now, timeZone);
}

export function isTodayOrFutureInTimeZone(date: Date, timeZone: string, now = new Date()) {
  return businessDateKey(date, timeZone) >= businessDateKey(now, timeZone);
}

function twoDigits(value: number) {
  return String(value).padStart(2, "0");
}
