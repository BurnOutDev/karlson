import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ExternalLink } from "lucide-react";
import { api, type Ingredient } from "@/api";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

const gel = (n: number) => `${n.toFixed(2)} ₾`;

export default function IngredientsPage() {
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<Ingredient | null>(null);
  const { data: ingredients, isLoading } = useQuery({ queryKey: ["ingredients"], queryFn: () => api.ingredients() });

  const filtered = useMemo(() => {
    const s = search.trim().toLowerCase();
    return (ingredients ?? []).filter(
      (i) => !s || i.name.toLowerCase().includes(s) || i.nameKa?.includes(s) || i.nameRu?.toLowerCase().includes(s) || i.category.includes(s),
    );
  }, [ingredients, search]);

  const withPrices = filtered.filter((i) => i.offers.length > 0).length;

  return (
    <div className="grid gap-4 lg:grid-cols-[1fr_380px]">
      <div className="grid gap-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <Input className="max-w-xs" placeholder="Search: ყველი, молоко, cheese…" value={search} onChange={(e) => setSearch(e.target.value)} />
          <p className="text-sm text-muted-foreground">
            {isLoading ? "Loading…" : `${filtered.length} ingredients, ${withPrices} with a local price`}
          </p>
        </div>
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Ingredient</TableHead>
                <TableHead>Category</TableHead>
                <TableHead className="text-right">Cheapest</TableHead>
                <TableHead className="text-right">per kg / l</TableHead>
                <TableHead className="text-right">Products</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map((i) => {
                const best = i.offers.reduce<typeof i.offers[number] | null>((a, o) => (a == null || o.minPrice < a.minPrice ? o : a), null);
                const perKg = i.offers.map((o) => o.minPricePerKg).filter((p): p is number => p != null);
                return (
                  <TableRow key={i.id} className="cursor-pointer" data-state={selected?.id === i.id ? "selected" : undefined} onClick={() => setSelected(i)}>
                    <TableCell>
                      <div className="font-medium">{i.name}</div>
                      <div className="text-xs text-muted-foreground">{[i.nameKa, i.nameRu].filter(Boolean).join(" · ")}</div>
                    </TableCell>
                    <TableCell><Badge variant="outline">{i.category}</Badge></TableCell>
                    <TableCell className="text-right">{best ? gel(best.minPrice) : <span className="text-muted-foreground">—</span>}</TableCell>
                    <TableCell className="text-right">{perKg.length ? gel(Math.min(...perKg)) : <span className="text-muted-foreground">—</span>}</TableCell>
                    <TableCell className="text-right">{i.offers.reduce((n, o) => n + o.productCount, 0) || <span className="text-muted-foreground">0</span>}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      </div>

      <aside>{selected ? <ProductList ingredient={selected} /> : <p className="text-sm text-muted-foreground">Pick an ingredient to see the products behind its price.</p>}</aside>
    </div>
  );
}

function ProductList({ ingredient }: { ingredient: Ingredient }) {
  const { data: products } = useQuery({ queryKey: ["ingredient-products", ingredient.id], queryFn: () => api.ingredientProducts(ingredient.id) });
  return (
    <div className="sticky top-4 grid gap-3">
      <div>
        <h2 className="text-lg font-semibold">{ingredient.name}</h2>
        <div className="flex flex-wrap gap-1">{ingredient.tags.map((t) => <Badge key={t} variant="secondary">{t}</Badge>)}</div>
      </div>
      <div className="grid max-h-[70vh] gap-2 overflow-y-auto pr-1">
        {products?.length === 0 && <p className="text-sm text-muted-foreground">No store sells this yet.</p>}
        {products?.map((p) => (
          <div key={`${p.storeId}:${p.externalId}`} className="rounded-md border p-2 text-sm">
            <div className="flex items-start justify-between gap-2">
              <div className="font-medium">{p.name}</div>
              {p.url && (
                <a href={p.url} target="_blank" rel="noreferrer" className="text-muted-foreground hover:text-foreground" title="Open in store">
                  <ExternalLink className="size-4" />
                </a>
              )}
            </div>
            <div className="flex flex-wrap items-center gap-x-3 text-muted-foreground">
              <span className="text-foreground">{gel(p.price)}</span>
              {p.unitQty && p.unit && <span>{p.unitQty} {p.unit}</span>}
              {p.pricePerKg != null && <span>{gel(p.pricePerKg)}/kg</span>}
              <span>{p.storeId}</span>
              {p.confidence < 0.7 && <Badge variant="outline">unsure</Badge>}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
