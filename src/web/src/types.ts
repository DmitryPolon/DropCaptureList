export type Session = {
  email: string;
  nickname: string;
  household: string;
  motto: string;
  logoLetter: string;
  isAppAdmin?: boolean;
};

export type HouseholdBrand = {
  name: string;
  motto: string;
  logoLetter: string;
};

export type HouseholdDirectory = {
  name: string;
  emails: string;
};

export type Member = {
  userId: string;
  email: string;
  nickname: string;
  isAppAdmin: boolean;
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
