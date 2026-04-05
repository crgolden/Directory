export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface ChatResponse {
  output: string | null;
  conversationId: string | null;
}

export interface ConversationDetails {
  conversationId: string;
  createdAt: number;
}

export interface ConversationItemSummary {
  id: string;
  role: string | null;
  text: string | null;
}
