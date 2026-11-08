import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, RefreshCw, Trash2 } from "lucide-react";
import { api, DIET_TYPES, type Household, type Member } from "@/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const dietLabel = (value: string) => DIET_TYPES.find((d) => d.value === value)?.label ?? value;

export default function HouseholdPage({ household }: { household: Household }) {
  const queryClient = useQueryClient();
  const onSuccess = (h: Household) => queryClient.setQueryData(["me"], h);

  const update = useMutation({
    mutationFn: ({ name, budget }: { name: string; budget: number | null }) => api.updateHousehold(name, budget),
    onSuccess,
  });
  const regenerate = useMutation({ mutationFn: api.regenerateInviteCode, onSuccess });
  const addMember = useMutation({ mutationFn: api.addMember, onSuccess });
  const updateMember = useMutation({
    mutationFn: ({ id, ...m }: Member) => api.updateMember(id, m),
    onSuccess,
  });
  const removeMember = useMutation({ mutationFn: api.removeMember, onSuccess });

  const [name, setName] = useState(household.name);
  const [budget, setBudget] = useState(household.weeklyBudgetGel?.toString() ?? "");
  const [newMember, setNewMember] = useState({ name: "", dietType: "normal", notes: "" });

  const error = update.error ?? addMember.error ?? updateMember.error ?? removeMember.error;

  return (
    <div className="grid gap-6 md:grid-cols-2">
      <Card>
        <CardHeader>
          <CardTitle>Household</CardTitle>
          <CardDescription>Name, weekly budget and the code others use to join.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="hname">Name</Label>
            <Input id="hname" value={name} onChange={(e) => setName(e.target.value)} maxLength={60} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="budget">Weekly budget (GEL)</Label>
            <Input id="budget" type="number" min={0} step={10} value={budget} onChange={(e) => setBudget(e.target.value)} placeholder="e.g. 300" />
          </div>
          <Button
            onClick={() => update.mutate({ name, budget: budget === "" ? null : Number(budget) })}
            disabled={update.isPending || (name === household.name && (budget === "" ? null : Number(budget)) === household.weeklyBudgetGel)}
          >
            Save
          </Button>

          <div className="mt-2 flex items-center justify-between rounded-md border p-3">
            <div>
              <div className="text-xs text-muted-foreground">Invite code</div>
              <div className="font-mono text-2xl tracking-widest">{household.inviteCode}</div>
            </div>
            <Button variant="ghost" size="icon" onClick={() => regenerate.mutate()} title="Generate a new code (the old one stops working)">
              <RefreshCw className="size-4" />
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Members</CardTitle>
          <CardDescription>Each person's diet shapes the plan; one shopping list covers everyone.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          {household.members.length === 0 && <p className="text-sm text-muted-foreground">No members yet.</p>}
          {household.members.map((m) => (
            <div key={m.id} className="grid gap-2 rounded-md border p-3">
              <div className="flex items-center justify-between gap-2">
                <Input
                  className="h-8"
                  defaultValue={m.name}
                  maxLength={60}
                  onBlur={(e) => e.target.value !== m.name && updateMember.mutate({ ...m, name: e.target.value })}
                />
                <Button variant="ghost" size="icon" onClick={() => removeMember.mutate(m.id)} title="Remove">
                  <Trash2 className="size-4" />
                </Button>
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <select
                  className="h-8 rounded-md border bg-background px-2 text-sm"
                  value={m.dietType}
                  onChange={(e) => updateMember.mutate({ ...m, dietType: e.target.value })}
                >
                  {DIET_TYPES.map((d) => (
                    <option key={d.value} value={d.value}>{d.label}</option>
                  ))}
                </select>
                <Badge variant="secondary">{dietLabel(m.dietType)}</Badge>
              </div>
              <Input
                className="h-8"
                defaultValue={m.notes ?? ""}
                placeholder="Notes: allergies, dislikes, no pork…"
                maxLength={500}
                onBlur={(e) => e.target.value !== (m.notes ?? "") && updateMember.mutate({ ...m, notes: e.target.value || null })}
              />
            </div>
          ))}

          <form
            className="grid gap-2 rounded-md border border-dashed p-3"
            onSubmit={(e) => {
              e.preventDefault();
              addMember.mutate({ name: newMember.name, dietType: newMember.dietType, notes: newMember.notes || null });
              setNewMember({ name: "", dietType: "normal", notes: "" });
            }}
          >
            <div className="flex gap-2">
              <Input
                className="h-8"
                value={newMember.name}
                onChange={(e) => setNewMember({ ...newMember, name: e.target.value })}
                placeholder="New member's name"
                maxLength={60}
              />
              <select
                className="h-8 rounded-md border bg-background px-2 text-sm"
                value={newMember.dietType}
                onChange={(e) => setNewMember({ ...newMember, dietType: e.target.value })}
              >
                {DIET_TYPES.map((d) => (
                  <option key={d.value} value={d.value}>{d.label}</option>
                ))}
              </select>
            </div>
            <Button type="submit" size="sm" variant="secondary" disabled={!newMember.name.trim() || addMember.isPending}>
              <Plus className="size-4" /> Add member
            </Button>
          </form>

          {error && <p className="text-sm text-destructive">{error.message}</p>}
        </CardContent>
      </Card>
    </div>
  );
}
