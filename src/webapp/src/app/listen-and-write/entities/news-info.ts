export interface NewsInfo {
  id: string;
  title: string;
  description?: string | null;
  url: string;
  imageUrl: string;
  text: string;
  textLength: number;
}
