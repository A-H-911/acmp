import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { RequireRole } from './ProtectedRoute';
import { renderWithAuth } from '../test/render';

describe('RequireRole', () => {
  it('renders children when the user holds a required role', () => {
    renderWithAuth(<RequireRole roles={['administrator']}>secret admin area</RequireRole>, {
      roles: ['administrator'],
    });
    expect(screen.getByText('secret admin area')).toBeInTheDocument();
  });

  it('renders a 403 state when the user lacks the role', () => {
    renderWithAuth(<RequireRole roles={['administrator']}>secret admin area</RequireRole>, {
      roles: ['member'],
    });
    expect(screen.queryByText('secret admin area')).not.toBeInTheDocument();
    expect(screen.getByText('Access denied')).toBeInTheDocument();
  });
});
