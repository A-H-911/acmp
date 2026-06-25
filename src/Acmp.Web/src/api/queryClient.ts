import { QueryClient } from '@tanstack/react-query';
import { ApiError } from './apiClient';

/*
 * Shared TanStack Query client. Server state is owned here, not duplicated into
 * component state. Don't retry on 4xx (auth/validation won't fix themselves);
 * one retry covers transient 5xx/network blips.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: (failureCount, error) => {
        if (error instanceof ApiError && error.status < 500) return false;
        return failureCount < 1;
      },
    },
  },
});
