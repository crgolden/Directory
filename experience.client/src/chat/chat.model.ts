export interface Chat {
  chatId: string;
  title: string | null;
  createdAt: number;
}

export interface ChatHistoryMessage {
  role: string | null;
  text: string | null;
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface ChatResponse {
  output: string | null;
  chatId: string;
}
