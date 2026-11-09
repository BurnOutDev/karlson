import { useQuery } from "@tanstack/react-query";
import { Navigate, NavLink, Route, Routes } from "react-router-dom";
import { Database, Home, LogOut } from "lucide-react";
import { api, ApiError } from "@/api";
import { cn } from "@/lib/utils";
import JoinPage from "@/pages/JoinPage";
import HouseholdPage from "@/pages/HouseholdPage";
import IngredientsPage from "@/pages/IngredientsPage";
import { useQueryClient } from "@tanstack/react-query";

export function useHousehold() {
  return useQuery({
    queryKey: ["me"],
    queryFn: api.me,
    retry: false,
    // 401 just means "not joined yet"; don't treat it as an error state.
    throwOnError: (error) => !(error instanceof ApiError && error.status === 401),
  });
}

export default function App() {
  const { data: household, isLoading } = useHousehold();
  const queryClient = useQueryClient();

  if (isLoading) return <div className="p-8 text-muted-foreground">Loading…</div>;

  if (!household) {
    return (
      <Routes>
        <Route path="/join" element={<JoinPage />} />
        <Route path="*" element={<Navigate to="/join" replace />} />
      </Routes>
    );
  }

  const leave = async () => {
    await api.leaveHousehold();
    queryClient.setQueryData(["me"], null);
    queryClient.invalidateQueries({ queryKey: ["me"] });
  };

  const link = ({ isActive }: { isActive: boolean }) =>
    cn(
      "flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium",
      isActive ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
    );

  return (
    <div className="mx-auto flex min-h-screen max-w-5xl flex-col">
      <header className="flex items-center justify-between gap-4 border-b px-4 py-3">
        <div className="min-w-0 truncate font-semibold">Karlson · {household.name}</div>
        <nav className="flex items-center gap-1">
          <NavLink to="/household" className={link}>
            <Home className="size-4" /> <span className="hidden sm:inline">Household</span>
          </NavLink>
          <NavLink to="/data/ingredients" className={link} title="Catalog data behind the plans: ingredients and store prices">
            <Database className="size-4" /> <span className="hidden sm:inline">Data</span>
          </NavLink>
          <button onClick={leave} className={link({ isActive: false })} title="Leave this household on this device">
            <LogOut className="size-4" />
          </button>
        </nav>
      </header>
      <main className="flex-1 px-4 py-6">
        <Routes>
          <Route path="/household" element={<HouseholdPage household={household} />} />
          <Route path="/data/ingredients" element={<IngredientsPage />} />
          <Route path="/ingredients" element={<Navigate to="/data/ingredients" replace />} />
          <Route path="*" element={<Navigate to="/household" replace />} />
        </Routes>
      </main>
    </div>
  );
}
