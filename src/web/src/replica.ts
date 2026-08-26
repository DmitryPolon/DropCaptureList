import type { ListItem } from "./types";

export type ReplicaCell = {
  item: ListItem | null;
};

export type ReplicaSheet = {
  createdAt: string;
  columnCount: number;
  rows: ReplicaCell[][];
};

export function sheetsFromItems(items: ListItem[]): { sheets: ReplicaSheet[]; leftover: ListItem[] } {
  const leftover: ListItem[] = [];
  const batches = new Map<string, ListItem[]>();

  for (const item of items) {
    if (item.excelRow > 0 && item.excelColumn > 0) {
      const key = item.createdAt;
      const batch = batches.get(key) ?? [];
      batch.push(item);
      batches.set(key, batch);
    } else {
      leftover.push(item);
    }
  }

  const sheets = [...batches.entries()]
    .sort((a, b) => (a[0] < b[0] ? 1 : -1))
    .map(([, batch]) => toSheet(batch));

  return { sheets, leftover };
}

function toSheet(batch: ListItem[]): ReplicaSheet {
  const minRow = Math.min(...batch.map((item) => item.excelRow));
  const maxRow = Math.max(...batch.map((item) => item.excelRow));
  const minCol = Math.min(...batch.map((item) => item.excelColumn));
  const maxCol = Math.max(...batch.map((item) => item.excelColumn));
  const lookup = new Map<string, ListItem>();
  for (const item of batch) {
    lookup.set(`${item.excelRow}:${item.excelColumn}`, item);
  }

  const rows: ReplicaCell[][] = [];
  for (let row = minRow; row <= maxRow; row++) {
    const cells: ReplicaCell[] = [];
    for (let col = minCol; col <= maxCol; col++) {
      cells.push({ item: lookup.get(`${row}:${col}`) ?? null });
    }
    rows.push(cells);
  }

  return {
    createdAt: batch[0]?.createdAt ?? "",
    columnCount: maxCol - minCol + 1,
    rows
  };
}
