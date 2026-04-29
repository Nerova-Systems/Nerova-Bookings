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

function twoDigits(value: number) {
  return String(value).padStart(2, "0");
}
