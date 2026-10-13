import type { components } from '$lib/api/v1';
import type { OverviewUrlState } from '$lib/components/dashboard/overview-url';

declare global {
	namespace App {
		interface PageState {
			overview?: OverviewUrlState;
			overviewReveal?: number;
		}
		interface Locals {
			user: components['schemas']['UserResponse'] | null;
			locale: string;
		}
	}

	interface Window {
		turnstile?: {
			render: (container: HTMLElement, options: Record<string, unknown>) => string;
			reset: (widgetId: string) => void;
			remove: (widgetId: string) => void;
		};
	}
}

export {};
