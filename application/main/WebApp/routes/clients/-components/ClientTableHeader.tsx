import { Trans } from "@lingui/react/macro";
import { TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { ArrowUp } from "lucide-react";

import { SortableClientProperties } from "@/shared/lib/api/client";

export type SortDescriptor = {
  column: string;
  direction: "ascending" | "descending";
};

interface ClientTableHeaderProps {
  sortDescriptor: SortDescriptor;
  isMobile: boolean;
  onSortChange: (columnId: string) => void;
}

export function ClientTableHeader({ sortDescriptor, isMobile, onSortChange }: Readonly<ClientTableHeaderProps>) {
  return (
    <TableHeader className="z-10 bg-inherit sm:sticky sm:top-0">
      <TableRow>
        <TableHead
          data-column={SortableClientProperties.Name}
          onClick={() => onSortChange(SortableClientProperties.Name)}
        >
          <Trans>Name</Trans>
          <SortIndicator sortDescriptor={sortDescriptor} columnId={SortableClientProperties.Name} />
        </TableHead>
        {!isMobile && (
          <>
            <TableHead
              data-column={SortableClientProperties.Email}
              onClick={() => onSortChange(SortableClientProperties.Email)}
            >
              <Trans>Email</Trans>
              <SortIndicator sortDescriptor={sortDescriptor} columnId={SortableClientProperties.Email} />
            </TableHead>
            <TableHead className="w-[10rem] min-w-[6rem]">
              <Trans>Phone</Trans>
            </TableHead>
            <TableHead
              data-column={SortableClientProperties.FirstVisitAt}
              className="w-[7.5rem] min-w-[4rem]"
              onClick={() => onSortChange(SortableClientProperties.FirstVisitAt)}
            >
              <Trans>First visit</Trans>
              <SortIndicator sortDescriptor={sortDescriptor} columnId={SortableClientProperties.FirstVisitAt} />
            </TableHead>
            <TableHead
              data-column={SortableClientProperties.LastVisitAt}
              className="w-[7.5rem] min-w-[4rem]"
              onClick={() => onSortChange(SortableClientProperties.LastVisitAt)}
            >
              <Trans>Last visit</Trans>
              <SortIndicator sortDescriptor={sortDescriptor} columnId={SortableClientProperties.LastVisitAt} />
            </TableHead>
          </>
        )}
      </TableRow>
    </TableHeader>
  );
}

interface SortIndicatorProps {
  sortDescriptor: SortDescriptor;
  columnId: string;
}

function SortIndicator({ sortDescriptor, columnId }: Readonly<SortIndicatorProps>) {
  if (sortDescriptor.column !== columnId) {
    return null;
  }

  return (
    <span
      className={`flex size-4 items-center justify-center transition ${sortDescriptor.direction === "descending" ? "rotate-180" : ""}`}
    >
      <ArrowUp aria-hidden={true} className="size-4 text-muted-foreground" />
    </span>
  );
}
