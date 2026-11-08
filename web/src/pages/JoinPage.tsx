import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type Household } from "@/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

export default function JoinPage() {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [code, setCode] = useState("");

  const onSuccess = (household: Household) => queryClient.setQueryData(["me"], household);
  const create = useMutation({ mutationFn: api.createHousehold, onSuccess });
  const join = useMutation({ mutationFn: api.joinHousehold, onSuccess });

  return (
    <div className="mx-auto grid max-w-3xl gap-6 p-6 pt-16 md:grid-cols-2">
      <div className="md:col-span-2">
        <h1 className="text-2xl font-semibold">Karlson</h1>
        <p className="text-muted-foreground">Weekly meal plans priced from what Tbilisi stores actually sell.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Join a household</CardTitle>
          <CardDescription>Someone in your household already has an invite code.</CardDescription>
        </CardHeader>
        <CardContent>
          <form
            className="grid gap-3"
            onSubmit={(e) => {
              e.preventDefault();
              join.mutate(code);
            }}
          >
            <Label htmlFor="code">Invite code</Label>
            <Input id="code" value={code} onChange={(e) => setCode(e.target.value.toUpperCase())} placeholder="e.g. K7M2PQ" maxLength={6} />
            {join.error && <p className="text-sm text-destructive">{join.error.message}</p>}
            <Button type="submit" disabled={code.length !== 6 || join.isPending}>Join</Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Start a new household</CardTitle>
          <CardDescription>You'll get an invite code to share with the others.</CardDescription>
        </CardHeader>
        <CardContent>
          <form
            className="grid gap-3"
            onSubmit={(e) => {
              e.preventDefault();
              create.mutate(name);
            }}
          >
            <Label htmlFor="name">Household name</Label>
            <Input id="name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Murusidze family" maxLength={60} />
            {create.error && <p className="text-sm text-destructive">{create.error.message}</p>}
            <Button type="submit" variant="secondary" disabled={!name.trim() || create.isPending}>Create</Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
