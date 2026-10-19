/** Freeze all selection changes, including external tree deselection, while an editor is open. */
export function selectionWhileEditing(
	editingEmis: string | null,
	selected: string | null
): string | null {
	return editingEmis ?? selected;
}
