import { inject } from '@angular/core';
import { ResolveFn } from '@angular/router';
import { Chat } from './chat.model';
import { ChatService } from './chat.service';

export const chatsResolver: ResolveFn<Chat[]> = () =>
  inject(ChatService).getChats();
