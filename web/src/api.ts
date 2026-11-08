// Thin typed client for the Karlson API. Cookies carry the household session.

export type Member = { id: string; name: string; dietType: string; notes: string | null };
export type Household = {
  id: string;
  name: string;
  inviteCode: string;
  weeklyBudgetGel: number | null;
  members: Member[];
};
export type StoreOffer = { storeId: string; productCount: number; minPrice: number; minPricePerKg: number | null };
export type Ingredient = {
  id: number;
  name: string;
  nameKa: string | null;
  nameRu: string | null;
  category: string;
  tags: string[];
  offers: StoreOffer[];
};
export type Product = {
  storeId: string;
  externalId: string;
  name: string;
  brand: string | null;
  price: number;
  unitQty: number | null;
  unit: string | null;
  pricePerKg: number | null;
  url: string | null;
  imageUrl: string | null;
  confidence: number;
};
export type Store = { id: string; name: string; type: string; url: string | null };

export const DIET_TYPES: { value: string; label: string }[] = [
  { value: "normal", label: "Normal" },
  { value: "glp1", label: "GLP-1 (high protein, small portions)" },
  { value: "keto", label: "Keto" },
  { value: "kid", label: "Kid" },
  { value: "vegetarian", label: "Vegetarian" },
  { value: "vegan", label: "Vegan" },
  { value: "gluten_free", label: "Gluten-free" },
];

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: "same-origin",
  });
  if (!res.ok) {
    let message = res.statusText;
    try {
      const data = await res.json();
      message = typeof data === "string" ? data : (data.detail ?? data.title ?? message);
    } catch {
      /* no body */
    }
    throw new ApiError(res.status, message);
  }
  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

export const api = {
  me: () => request<Household>("GET", "/api/me"),
  createHousehold: (name: string) => request<Household>("POST", "/api/households", { name }),
  joinHousehold: (inviteCode: string) => request<Household>("POST", "/api/households/join", { inviteCode }),
  leaveHousehold: () => request<void>("POST", "/api/households/leave"),
  updateHousehold: (name: string, weeklyBudgetGel: number | null) =>
    request<Household>("PUT", "/api/me", { name, weeklyBudgetGel }),
  addMember: (m: Omit<Member, "id">) => request<Household>("POST", "/api/me/members", m),
  updateMember: (id: string, m: Omit<Member, "id">) => request<Household>("PUT", `/api/me/members/${id}`, m),
  removeMember: (id: string) => request<Household>("DELETE", `/api/me/members/${id}`),
  regenerateInviteCode: () => request<Household>("POST", "/api/me/invite-code"),

  stores: () => request<Store[]>("GET", "/api/stores"),
  ingredients: (search?: string) =>
    request<Ingredient[]>("GET", `/api/ingredients${search ? `?search=${encodeURIComponent(search)}` : ""}`),
  ingredientProducts: (id: number) => request<Product[]>("GET", `/api/ingredients/${id}/products`),
};
