export type Session = {
  email: string;
  nickname: string;
  household: string;
  motto: string;
  logoLetter: string;
};

export type ListItem = {
  id: string;
  text: string;
  nickname: string;
  createdAt: string;
  isCompleted: boolean;
  completedByNickname: string | null;
  completedAt: string | null;
  excelRow: number;
  excelColumn: number;
  isBold: boolean;
  fontColor: string | null;
  fillColor: string | null;
};
