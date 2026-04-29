export type ClientFlag = "alert" | "overdue" | "blocked" | null;

export interface Client {
  initials: string;
  name: string;
  phone: string;
  visits: number;
  lifetime: string;
  lastVisit: string;
  status: string;
  flag: ClientFlag;
}

export const CLIENTS: Client[] = [
  {
    initials: "LB",
    name: "Liam Botha",
    phone: "+27 82 341 7890",
    visits: 12,
    lifetime: "R 5 280",
    lastVisit: "22 Apr",
    status: "VIP",
    flag: "alert"
  },
  {
    initials: "TK",
    name: "Thandi Khoza",
    phone: "+27 73 210 5544",
    visits: 5,
    lifetime: "R 1 100",
    lastVisit: "22 Apr",
    status: "Active",
    flag: "overdue"
  },
  {
    initials: "PW",
    name: "Pieter de Wet",
    phone: "+27 84 908 2211",
    visits: 9,
    lifetime: "R 2 850",
    lastVisit: "22 Apr",
    status: "Active",
    flag: null
  },
  {
    initials: "RM",
    name: "Refilwe Mthembu",
    phone: "+27 79 443 0012",
    visits: 3,
    lifetime: "R 1 350",
    lastVisit: "23 Apr",
    status: "New",
    flag: null
  },
  {
    initials: "ME",
    name: "Marco Esposito",
    phone: "+27 72 661 8830",
    visits: 7,
    lifetime: "R 1 540",
    lastVisit: "23 Apr",
    status: "Active",
    flag: null
  },
  {
    initials: "AP",
    name: "Aisha Patel",
    phone: "+27 83 553 7741",
    visits: 4,
    lifetime: "R 1 800",
    lastVisit: "24 Apr",
    status: "Active",
    flag: "overdue"
  },
  {
    initials: "SS",
    name: "Sipho Sithole",
    phone: "+27 71 225 6699",
    visits: 2,
    lifetime: "R 440",
    lastVisit: "25 Apr",
    status: "New",
    flag: null
  },
  {
    initials: "AV",
    name: "Ayanda van Niekerk",
    phone: "+27 82 774 3308",
    visits: 6,
    lifetime: "R 1 320",
    lastVisit: "25 Apr",
    status: "Active",
    flag: null
  },
  {
    initials: "ON",
    name: "Olivia Nkosi",
    phone: "+27 73 889 0054",
    visits: 1,
    lifetime: "R 650",
    lastVisit: "25 Apr",
    status: "New",
    flag: null
  },
  {
    initials: "DN",
    name: "David Ndlovu",
    phone: "+27 82 334 6621",
    visits: 18,
    lifetime: "R 8 100",
    lastVisit: "18 Apr",
    status: "VIP",
    flag: null
  },
  {
    initials: "FP",
    name: "Fatima Parker",
    phone: "+27 71 002 5543",
    visits: 0,
    lifetime: "R 0",
    lastVisit: "—",
    status: "Blocked",
    flag: "blocked"
  }
];

export type Filter = "all" | "vip" | "new" | "blocked";

export const FILTER_LABELS: Record<Filter, string> = {
  all: "All clients",
  vip: "VIP",
  new: "New",
  blocked: "Blocked"
};

export const FILTER_COUNTS: Record<Filter, number> = {
  all: CLIENTS.length,
  vip: CLIENTS.filter((c) => c.status === "VIP").length,
  new: CLIENTS.filter((c) => c.status === "New").length,
  blocked: CLIENTS.filter((c) => c.status === "Blocked").length
};

export function FlagDot({ flag }: { flag: ClientFlag }) {
  if (flag === "alert")
    return (
      <span className="inline-flex size-5 items-center justify-center rounded-full bg-warning/12 text-[11px] text-warning">
        !
      </span>
    );
  if (flag === "overdue")
    return (
      <span className="inline-flex size-5 items-center justify-center rounded-full bg-destructive/12 text-[11px] text-destructive">
        $
      </span>
    );
  if (flag === "blocked")
    return (
      <span className="inline-flex size-5 items-center justify-center rounded-full bg-black/8 text-[11px] text-muted-foreground">
        ✕
      </span>
    );
  return null;
}
