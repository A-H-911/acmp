import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MeetingRecording } from './MeetingRecording';

describe('MeetingRecording (P6a — Webex Phase 2 defer)', () => {
  it('renders the honest Webex-deferred gate', () => {
    render(<MeetingRecording />);
    expect(screen.getByText('Recording is a later-phase integration')).toBeInTheDocument();
  });
});
