export function ActiveStatusBadge({ isActive }: { isActive: boolean }) {
  return <span className={`status-badge status-${isActive ? 'active' : 'inactive'}`}>{isActive ? 'Active' : 'Inactive'}</span>;
}
