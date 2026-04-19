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

/**
 * Context about the product the user is creating or editing. Passed into the
 * manual-chat panel so that new chats can be auto-titled with the product name
 * / brand / model and the assistant has the details it needs to find a manual.
 */
export interface ProductContext {
  id: string | null;
  name: string | null;
  brand: string | null;
  modelNumber: string | null;
}
