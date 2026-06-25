/*
 * App-level error boundary. React has no functional equivalent, so this stays a
 * class component. It shows a user-friendly state with no technical detail
 * (docs/14 page 92) and a recovery action; the raw error is logged to the
 * console for diagnostics only.
 */
import { Component, type ErrorInfo, type ReactNode } from 'react';
import { ErrorState } from './states';

interface Props { children: ReactNode; }
interface State { hasError: boolean; }

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    // Diagnostics only — never surfaced to the user (no internal details leak).
    console.error('Unhandled UI error', error, info.componentStack);
  }

  private reset = () => this.setState({ hasError: false });

  render(): ReactNode {
    if (this.state.hasError) {
      return (
        <main className="app-main">
          <div className="page">
            <ErrorState onRetry={this.reset} />
          </div>
        </main>
      );
    }
    return this.props.children;
  }
}
