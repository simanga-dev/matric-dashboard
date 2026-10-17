import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { SchoolList } from '$lib/types/dashboard';
import { scheduleSchoolTreeSearch } from './school-tree-search';

const result: SchoolList = {
	items: [],
	totalCount: 250,
	pageNumber: 1,
	pageSize: 100,
	totalPages: 3,
	hasPreviousPage: false,
	hasNextPage: true
};

describe('school tree server search', () => {
	beforeEach(() => vi.useFakeTimers());
	afterEach(() => vi.useRealTimers());

	it('debounces typing and requests a bounded page with the trimmed query', async () => {
		const fetchPage = vi.fn().mockResolvedValue(result);
		const onResult = vi.fn();
		scheduleSchoolTreeSearch('  Eastern Cape  ', 1, onResult, vi.fn(), fetchPage);
		await vi.advanceTimersByTimeAsync(149);
		expect(fetchPage).not.toHaveBeenCalled();
		await vi.advanceTimersByTimeAsync(1);
		expect(fetchPage).toHaveBeenCalledWith(1, 100, 'Eastern Cape', expect.any(AbortSignal));
		expect(onResult).toHaveBeenCalledWith(result);
	});

	it('does not send superseded keystrokes', async () => {
		const fetchPage = vi.fn().mockResolvedValue(result);
		const cancel = scheduleSchoolTreeSearch('sch', 1, vi.fn(), vi.fn(), fetchPage);
		cancel();
		scheduleSchoolTreeSearch('school', 1, vi.fn(), vi.fn(), fetchPage);
		await vi.runAllTimersAsync();
		expect(fetchPage).toHaveBeenCalledTimes(1);
		expect(fetchPage.mock.calls[0]?.[2]).toBe('school');
	});

	it('aborts in-flight requests and ignores their late results', async () => {
		let resolve!: (value: SchoolList) => void;
		const fetchPage = vi.fn<NonNullable<Parameters<typeof scheduleSchoolTreeSearch>[4]>>(
			() => new Promise<SchoolList>((done) => (resolve = done))
		);
		const onResult = vi.fn();
		const cancel = scheduleSchoolTreeSearch('old', 1, onResult, vi.fn(), fetchPage);
		await vi.advanceTimersByTimeAsync(150);
		cancel();
		expect(fetchPage.mock.calls[0]?.[3].aborted).toBe(true);
		resolve(result);
		await vi.runAllTimersAsync();
		expect(onResult).not.toHaveBeenCalled();
	});

	it('does not report errors from cancelled requests', async () => {
		let reject!: (error: Error) => void;
		const fetchPage = vi.fn(() => new Promise<SchoolList>((_, fail) => (reject = fail)));
		const onError = vi.fn();
		const cancel = scheduleSchoolTreeSearch('old', 1, vi.fn(), onError, fetchPage);
		await vi.advanceTimersByTimeAsync(150);
		cancel();
		reject(new Error('Aborted'));
		await vi.runAllTimersAsync();
		expect(onError).not.toHaveBeenCalled();
	});

	it('reports server failures for retry', async () => {
		const onError = vi.fn();
		scheduleSchoolTreeSearch(
			'school',
			1,
			vi.fn(),
			onError,
			vi.fn().mockRejectedValue(new Error('Offline'))
		);
		await vi.runAllTimersAsync();
		expect(onError).toHaveBeenCalledOnce();
	});

	it('loads subsequent pages without the typing delay', async () => {
		const fetchPage = vi.fn().mockResolvedValue(result);
		scheduleSchoolTreeSearch('school', 2, vi.fn(), vi.fn(), fetchPage);
		await vi.advanceTimersByTimeAsync(0);
		expect(fetchPage).toHaveBeenCalledWith(2, 100, 'school', expect.any(AbortSignal));
	});
});
