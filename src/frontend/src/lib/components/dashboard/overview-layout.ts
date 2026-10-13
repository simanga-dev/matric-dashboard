/** Panel widths (in px) for the overview workspace, keeping expanded sides stable. */
export const COLLAPSED_RAIL_WIDTH = 40;

export interface OverviewWidths {
	rail: number;
	tree: number;
	map: number;
	details: number;
}

const HANDLE_WIDTH = 8;

/**
 * Resolve each panel's width so a collapsed side becomes a thin rail while the
 * other expanded side keeps the width it had in the fully expanded layout.
 * The map absorbs the freed space.
 */
export function overviewWidths(
	workspaceWidth: number,
	sizes: number[],
	treeCollapsed: boolean,
	detailsCollapsed: boolean
): OverviewWidths {
	const rail = COLLAPSED_RAIL_WIDTH;
	const width = Math.max(0, workspaceWidth);
	const handles = (treeCollapsed ? 0 : 1) + (detailsCollapsed ? 0 : 1);
	// Expanded proportions are measured against the two-splitter layout.
	const expanded = Math.max(0, width - HANDLE_WIDTH * 2);
	const percent = (index: number) =>
		(Math.min(100, Math.max(0, sizes[index] ?? 0)) / 100) * expanded;
	const tree = treeCollapsed ? Math.min(rail, width) : percent(0);
	const details = detailsCollapsed ? Math.min(rail, width) : percent(2);
	const map = Math.max(0, width - tree - details - handles * HANDLE_WIDTH);
	return { rail, tree, map, details };
}
