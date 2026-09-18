export function Pagination({ page, pageSize, totalCount, onPage }: { page: number; pageSize: number; totalCount: number; onPage(page: number): void }) {
  const pages = Math.max(1, Math.ceil(totalCount / pageSize));
  return <div className="pagination" aria-label="Pagination"><span>Page {page} of {pages} · {totalCount} total</span><div><button className="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</button><button className="secondary" disabled={page >= pages} onClick={() => onPage(page + 1)}>Next</button></div></div>;
}
