import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ExternalLink } from "lucide-react";
import { api, type Ingredient, type Product } from "@/api";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

const gel = (n: number) => `${n.toFixed(2)} ₾`;
const perUnitLabel: Record<string, string> = { g: "kg", ml: "l", pcs: "pc" };
const dash = <span className="text-muted-foreground">—</span>;

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
      <div className="grid min-w-0 gap-3">
        <div>
          <h1 className="text-lg font-semibold">Ingredients and store prices</h1>
          <p className="text-sm text-muted-foreground">The catalog meal plans are built from. Pick a row to see the products behind its price.</p>
        </div>
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
                <TableHead className="hidden sm:table-cell">Category</TableHead>
                <TableHead className="text-right">Cheapest</TableHead>
                <TableHead className="hidden text-right sm:table-cell">per kg / l</TableHead>
                <TableHead className="hidden text-right sm:table-cell">Products</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.flatMap((i) => {
                const best = i.offers.reduce<typeof i.offers[number] | null>((a, o) => (a == null || o.minPrice < a.minPrice ? o : a), null);
                const perKg = i.offers.map((o) => o.minPricePerKg).filter((p): p is number => p != null);
                const minPerKg = perKg.length ? Math.min(...perKg) : null;
                const productCount = i.offers.reduce((n, o) => n + o.productCount, 0);
                const isSelected = selected?.id === i.id;

                const row = (
                  <TableRow key={i.id} className="cursor-pointer" data-state={isSelected ? "selected" : undefined} onClick={() => setSelected(isSelected ? null : i)}>
                    <TableCell className="whitespace-normal">
                      <div className="font-medium">{i.name}</div>
                      <div className="text-xs text-muted-foreground">{[i.nameKa, i.nameRu].filter(Boolean).join(" · ")}</div>
                      {/* Phone: the columns that do not fit move under the name. */}
                      <div className="text-xs text-muted-foreground sm:hidden">
                        {[i.category, minPerKg != null ? `${gel(minPerKg)}/${i.defaultUnit === "ml" ? "l" : "kg"}` : null, `${productCount} products`].filter(Boolean).join(" · ")}
                      </div>
                    </TableCell>
                    <TableCell className="hidden sm:table-cell"><Badge variant="outline">{i.category}</Badge></TableCell>
                    <TableCell className="text-right align-top sm:align-middle">{best ? gel(best.minPrice) : dash}</TableCell>
                    <TableCell className="hidden text-right sm:table-cell">{minPerKg != null ? gel(minPerKg) : dash}</TableCell>
                    <TableCell className="hidden text-right sm:table-cell">{productCount || <span className="text-muted-foreground">0</span>}</TableCell>
                  </TableRow>
                );

                // Below the lg breakpoint there is no side panel: products open under the row.
                return isSelected
                  ? [row, (
                    <TableRow key={`${i.id}-products`} className="hover:bg-transparent lg:hidden">
                      <TableCell colSpan={5} className="whitespace-normal bg-muted/30 p-3"><ProductList ingredient={i} /></TableCell>
                    </TableRow>
                  )]
                  : [row];
              })}
            </TableBody>
          </Table>
        </div>
      </div>

      <aside className="hidden lg:block">
        {selected
          ? <div className="sticky top-4"><ProductList ingredient={selected} scroll /></div>
          : <p className="text-sm text-muted-foreground">Pick an ingredient to see the products behind its price.</p>}
      </aside>
    </div>
  );
}

function ProductList({ ingredient, scroll = false }: { ingredient: Ingredient; scroll?: boolean }) {
  const { data: products } = useQuery({ queryKey: ["ingredient-products", ingredient.id], queryFn: () => api.ingredientProducts(ingredient.id) });
  return (
    <div className="grid gap-3">
      <div>
        <h2 className="text-lg font-semibold">{ingredient.name}</h2>
        <div className="flex flex-wrap items-center gap-1">
          {ingredient.tags.map((t) => <Badge key={t} variant="secondary">{t}</Badge>)}
          <span className="text-xs text-muted-foreground">
            counted in {ingredient.defaultUnit}
            {ingredient.gramsPerPiece != null && `, 1 pc ≈ ${ingredient.gramsPerPiece} g`}
          </span>
        </div>
      </div>
      <div className={scroll ? "grid max-h-[70vh] gap-2 overflow-y-auto pr-1" : "grid gap-2"}>
        {products?.length === 0 && <p className="text-sm text-muted-foreground">No store sells this yet.</p>}
        {products?.map((p) => <ProductCard key={`${p.storeId}:${p.externalId}`} product={p} />)}
      </div>
    </div>
  );
}

function ProductCard({ product: p }: { product: Product }) {
  return (
    <div className="rounded-md border bg-background p-2 text-sm">
      <div className="flex items-start justify-between gap-2">
        <div className="font-medium">{p.name}</div>
        {p.url && (
          <a href={p.url} target="_blank" rel="noreferrer" className="text-muted-foreground hover:text-foreground" title="Open in store">
            <ExternalLink className="size-4" />
          </a>
        )}
      </div>
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-muted-foreground">
        <span className="text-foreground">
          {gel(p.price)}
          {p.regularPrice != null && <s className="ml-1.5 text-muted-foreground">{gel(p.regularPrice)}</s>}
        </span>
        {p.unitQty != null && p.unit && <span>{p.soldBy === "weight" ? "per " : ""}{p.unitQty} {p.unit}</span>}
        {p.pricePerUnit != null && p.unit && <span>{gel(p.pricePerUnit)}/{perUnitLabel[p.unit] ?? p.unit}</span>}
        <span>{p.storeId}</span>
        {p.regularPrice != null && <Badge variant="secondary">sale</Badge>}
        {p.soldBy === "weight" && <Badge variant="outline">by weight</Badge>}
        {p.confidence < 0.7 && <Badge variant="outline">unsure</Badge>}
      </div>
    </div>
  );
}
