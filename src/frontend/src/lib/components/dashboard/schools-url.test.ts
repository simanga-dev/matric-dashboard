import { describe, expect, it } from 'vitest';
import { parseSchoolsQuery, schoolsQueryString } from './schools-url';

describe('school directory URLs', () => {
	it('reads search, province, sort, order and page', () => {
		expect(
			parseSchoolsQuery(
				new URLSearchParams('search=pretoria&province=GAUTENG&sort=passRate&order=desc&page=3')
			)
		).toEqual({
			search: 'pretoria',
			province: 'GAUTENG',
			sort: 'passRate',
			order: 'desc',
			page: 3
		});
	});

	it('keeps the legacy search-only deep link working', () => {
		expect(parseSchoolsQuery(new URLSearchParams('search=pretoria'))).toEqual({
			search: 'pretoria',
			province: null,
			sort: null,
			order: null,
			page: 1
		});
	});

	it('drops unknown sorts, orphan orders and invalid pages', () => {
		expect(
			parseSchoolsQuery(new URLSearchParams('sort=drop%20table&order=desc&page=-2'))
		).toMatchObject({ sort: null, order: null, page: 1 });
		expect(parseSchoolsQuery(new URLSearchParams('sort=name&order=sideways'))).toMatchObject({
			sort: 'name',
			order: 'asc'
		});
		expect(parseSchoolsQuery(new URLSearchParams('page=1.5')).page).toBe(1);
	});

	it('serialises only non-default values', () => {
		expect(
			schoolsQueryString({ search: '', province: null, sort: null, order: null, page: 1 })
		).toBe('');
		expect(
			schoolsQueryString({
				search: 'St. Mary & John',
				province: 'NORTH WEST',
				sort: 'totalWrote',
				order: 'desc',
				page: 2
			})
		).toBe('search=St.+Mary+%26+John&province=NORTH+WEST&sort=totalWrote&order=desc&page=2');
	});
});
