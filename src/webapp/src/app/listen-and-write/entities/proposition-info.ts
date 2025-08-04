import { NewsInfo } from "./news-info";

export interface PropositionInfo {
  id: number;
  publishedOn: string;
  subjectId: string;
  complexityId: string;
  audioFileId: string;
  voice: string;
  text: string;
  textLength: number;
  title: string;
  imageFileId?: string | null;
  createdAt: string;
  newsInfo: NewsInfo;
}
