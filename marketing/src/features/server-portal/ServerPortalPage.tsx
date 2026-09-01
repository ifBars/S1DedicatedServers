import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Copy, KeyRound, LogOut, RefreshCw, ShieldCheck, Trash2 } from "lucide-react";
import Footer from "@/components/Footer";
import Header from "@/components/Header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  authUrl,
  buildPublicListingToml,
  createListing,
  getAccount,
  getListings,
  getProviders,
  logout,
  PortalApiError,
  revokeListing,
  rotateListing,
  type IssuedCredential,
  type PortalListing,
} from "./server-portal-api";

const ServerPortalPage = () => {
  const queryClient = useQueryClient();
  const [label, setLabel] = useState("");
  const [credential, setCredential] = useState<IssuedCredential | null>(null);
  const [copied, setCopied] = useState<"id" | "secret" | "toml" | null>(null);
  const [confirming, setConfirming] = useState<string | null>(null);
  const accountQuery = useQuery({ queryKey: ["portal-account"], queryFn: getAccount, retry: false });
  const providersQuery = useQuery({ queryKey: ["portal-providers"], queryFn: getProviders, staleTime: 60_000 });
  const listingsQuery = useQuery({
    queryKey: ["portal-listings"],
    queryFn: getListings,
    enabled: accountQuery.isSuccess,
    retry: false,
  });
  const signedOut = accountQuery.error instanceof PortalApiError && accountQuery.error.status === 401;

  const refreshListings = () => queryClient.invalidateQueries({ queryKey: ["portal-listings"] });
  const createMutation = useMutation({
    mutationFn: () => createListing(label.trim(), accountQuery.data!.csrfToken),
    onSuccess: (issued) => {
      setCredential(issued);
      setLabel("");
      void refreshListings();
    },
  });
  const rotateMutation = useMutation({
    mutationFn: (listingId: string) => rotateListing(listingId, accountQuery.data!.csrfToken),
    onSuccess: (issued) => {
      setCredential(issued);
      setConfirming(null);
      void refreshListings();
    },
  });
  const revokeMutation = useMutation({
    mutationFn: (listingId: string) => revokeListing(listingId, accountQuery.data!.csrfToken),
    onSuccess: () => {
      setConfirming(null);
      void refreshListings();
    },
  });
  const logoutMutation = useMutation({
    mutationFn: () => logout(accountQuery.data!.csrfToken),
    onSuccess: () => {
      setCredential(null);
      queryClient.clear();
      window.location.assign("/server-portal");
    },
  });

  const authStatus = useMemo(
    () => (typeof window === "undefined" ? null : new URLSearchParams(window.location.search).get("auth")),
    [],
  );
  const copy = async (value: string, target: "id" | "secret" | "toml") => {
    await navigator.clipboard.writeText(value);
    setCopied(target);
    window.setTimeout(() => setCopied(null), 1600);
  };

  return (
    <div className="min-h-screen overflow-x-hidden bg-[#10110f] text-[#f4eddf]">
      <Header />
      <main className="mx-auto max-w-[1240px] px-4 pb-24 pt-24 md:px-8">
        <section className="grid gap-5 border-b border-[#2b3025] pb-8 lg:grid-cols-[minmax(0,1fr)_360px] lg:items-end">
          <div>
            <div className="mb-4 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-[#9ac36d]">
              <ShieldCheck size={16} /> Account-linked directory access
            </div>
            <h1 className="max-w-4xl text-[clamp(2.7rem,5vw,5.4rem)] font-semibold leading-[0.94] tracking-[-0.04em]">
              Public listing portal
            </h1>
            <p className="mt-5 max-w-2xl text-base leading-7 text-[#b7c9a5] md:text-lg">
              Issue and revoke credentials for servers you choose to publish. Direct IP connections remain available without a listing.
            </p>
          </div>
          <p className="border-l border-[#3b4235] pl-5 text-sm leading-6 text-[#87947c]">
            A portal identity establishes operator accountability. It does not claim that a modified server binary is official or tamper-proof.
          </p>
        </section>

        {authStatus && authStatus !== "signed_in" && (
          <Notice tone="error">Sign-in did not complete ({authStatus.replaceAll("_", " ")}). Try again.</Notice>
        )}

        {accountQuery.isLoading && <Notice>Checking your operator session…</Notice>}
        {(signedOut || (!accountQuery.isLoading && !accountQuery.data)) && (
          <SignIn providers={providersQuery.data} loading={providersQuery.isLoading} />
        )}

        {accountQuery.data && (
          <div className="mt-9 grid gap-10">
            <section className="flex flex-col gap-4 border-b border-[#2b3025] pb-7 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-xs uppercase tracking-[0.16em] text-[#87947c]">Signed in as</p>
                <p className="mt-2 text-lg font-medium">
                  {accountQuery.data.identities.map((identity) => identity.display_name).join(" · ")}
                </p>
                <p className="mt-1 font-mono text-xs text-[#65705f]">Operator {accountQuery.data.operatorId}</p>
              </div>
              <Button
                variant="outline"
                onClick={() => logoutMutation.mutate()}
                disabled={logoutMutation.isPending}
                className="border-[#3b4235] bg-transparent text-[#dceccc] hover:bg-[#1b1d18] hover:text-white"
              >
                <LogOut size={16} /> Sign out
              </Button>
            </section>

            {credential && <CredentialHandoff credential={credential} copied={copied} onCopy={copy} onClose={() => setCredential(null)} />}

            <section className="grid gap-6 lg:grid-cols-[300px_minmax(0,1fr)]">
              <div>
                <h2 className="text-xl font-semibold">Create a listing key</h2>
                <p className="mt-2 text-sm leading-6 text-[#87947c]">
                  Use a recognizable internal label. The secret is shown once and stored only as a hash by the directory.
                </p>
              </div>
              <form
                className="flex flex-col gap-3 border-t border-[#30352d] pt-5 sm:flex-row"
                onSubmit={(event) => {
                  event.preventDefault();
                  if (label.trim()) createMutation.mutate();
                }}
              >
                <Input
                  value={label}
                  maxLength={80}
                  onChange={(event) => setLabel(event.target.value)}
                  placeholder="Production — US West"
                  aria-label="Listing label"
                  className="min-h-11 border-[#30352d] bg-[#141611] text-[#f4eddf] placeholder:text-[#65705f] focus-visible:ring-[#9ac36d]"
                />
                <Button
                  type="submit"
                  disabled={!label.trim() || createMutation.isPending}
                  className="min-h-11 shrink-0 bg-[#9ac36d] text-[#11140f] hover:bg-[#afd681]"
                >
                  <KeyRound size={16} /> {createMutation.isPending ? "Issuing…" : "Issue credential"}
                </Button>
              </form>
              {createMutation.error && <MutationError error={createMutation.error} />}
            </section>

            <section>
              <div className="mb-4 flex items-end justify-between gap-4">
                <div>
                  <h2 className="text-xl font-semibold">Your listings</h2>
                  <p className="mt-1 text-sm text-[#87947c]">Presence appears only while the matching server sends valid heartbeats.</p>
                </div>
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={() => void listingsQuery.refetch()}
                  className="text-[#b7c9a5] hover:bg-[#1b1d18] hover:text-white"
                >
                  <RefreshCw size={15} /> Refresh
                </Button>
              </div>
              <div className="overflow-hidden border-y border-[#30352d]">
                {listingsQuery.data?.map((listing) => (
                  <ListingRow
                    key={listing.id}
                    listing={listing}
                    confirming={confirming}
                    busy={rotateMutation.isPending || revokeMutation.isPending}
                    onConfirm={setConfirming}
                    onRotate={(id) => rotateMutation.mutate(id)}
                    onRevoke={(id) => revokeMutation.mutate(id)}
                  />
                ))}
                {listingsQuery.data?.length === 0 && (
                  <p className="py-12 text-center text-sm text-[#87947c]">No listing credentials issued yet.</p>
                )}
                {listingsQuery.isLoading && <p className="py-12 text-center text-sm text-[#87947c]">Loading listings…</p>}
              </div>
              {(rotateMutation.error || revokeMutation.error) && <MutationError error={rotateMutation.error ?? revokeMutation.error} />}
            </section>
          </div>
        )}
      </main>
      <Footer />
    </div>
  );
};

const SignIn = ({ providers, loading }: { providers?: Record<"discord" | "steam", boolean>; loading: boolean }) => (
  <section className="mt-10 grid gap-8 border-y border-[#30352d] py-8 lg:grid-cols-[300px_minmax(0,1fr)]">
    <div>
      <h2 className="text-xl font-semibold">Identify the operator</h2>
      <p className="mt-2 text-sm leading-6 text-[#87947c]">
        Sign in through an existing account. The portal stores the provider identity, not the provider access token.
      </p>
    </div>
    <div className="grid gap-3 sm:grid-cols-2">
      <ProviderLink href={authUrl("steam")} disabled={loading || providers?.steam !== true} label={providers?.steam ? "Continue with Steam" : "Steam availability pending"} />
      <ProviderLink href={authUrl("discord")} disabled={loading || providers?.discord !== true} label={providers?.discord ? "Continue with Discord" : "Discord setup pending"} />
    </div>
  </section>
);

const ProviderLink = ({ href, disabled, label }: { href: string; disabled: boolean; label: string }) =>
  disabled ? (
    <span className="flex min-h-14 items-center justify-center border border-[#2b3025] px-4 text-sm text-[#65705f]">{label}</span>
  ) : (
    <a href={href} className="flex min-h-14 items-center justify-center border border-[#4b5740] bg-[#171a14] px-4 text-sm font-semibold text-[#e8f2dc] transition-colors hover:border-[#9ac36d] hover:bg-[#1d2218]">
      {label}
    </a>
  );

const CredentialHandoff = ({
  credential,
  copied,
  onCopy,
  onClose,
}: {
  credential: IssuedCredential;
  copied: "id" | "secret" | "toml" | null;
  onCopy: (value: string, target: "id" | "secret" | "toml") => Promise<void>;
  onClose: () => void;
}) => {
  const toml = buildPublicListingToml(credential);
  return (
    <section className="border border-[#7d9b5d] bg-[#151a11] p-5 md:p-7">
      <div className="flex flex-col justify-between gap-3 sm:flex-row">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#9ac36d]">Copy now</p>
          <h2 className="mt-2 text-xl font-semibold">This secret will not be shown again</h2>
        </div>
        <Button variant="ghost" onClick={onClose} className="self-start text-[#b7c9a5] hover:bg-[#20261a] hover:text-white">Done</Button>
      </div>
      <div className="mt-5 grid gap-3">
        <CredentialLine label="Listing ID" value={credential.listingId} copied={copied === "id"} onCopy={() => onCopy(credential.listingId, "id")} />
        <CredentialLine label="Secret" value={credential.secret} copied={copied === "secret"} onCopy={() => onCopy(credential.secret, "secret")} secret />
      </div>
      <div className="mt-5 flex items-center justify-between gap-4 border-t border-[#38452d] pt-4">
        <p className="text-sm text-[#b7c9a5]">Paste the generated block into <code className="font-mono text-[#f4eddf]">server_config.toml</code>.</p>
        <Button size="sm" onClick={() => onCopy(toml, "toml")} className="shrink-0 bg-[#9ac36d] text-[#11140f] hover:bg-[#afd681]">
          {copied === "toml" ? <Check size={15} /> : <Copy size={15} />} {copied === "toml" ? "Copied" : "Copy TOML"}
        </Button>
      </div>
    </section>
  );
};

const CredentialLine = ({ label, value, copied, onCopy, secret = false }: { label: string; value: string; copied: boolean; onCopy: () => void; secret?: boolean }) => (
  <div className="grid gap-2 border-t border-[#30352d] pt-3 sm:grid-cols-[100px_minmax(0,1fr)_auto] sm:items-center">
    <span className="text-xs uppercase tracking-[0.14em] text-[#87947c]">{label}</span>
    <code className="min-w-0 overflow-hidden text-ellipsis whitespace-nowrap font-mono text-sm text-[#e8f2dc]" aria-label={secret ? "Listing secret" : undefined}>{value}</code>
    <Button size="sm" variant="ghost" onClick={onCopy} className="justify-self-start text-[#b7c9a5] hover:bg-[#20261a] hover:text-white sm:justify-self-end">
      {copied ? <Check size={14} /> : <Copy size={14} />} {copied ? "Copied" : "Copy"}
    </Button>
  </div>
);

const ListingRow = ({ listing, confirming, busy, onConfirm, onRotate, onRevoke }: { listing: PortalListing; confirming: string | null; busy: boolean; onConfirm: (value: string | null) => void; onRotate: (id: string) => void; onRevoke: (id: string) => void }) => {
  const rotateKey = `rotate:${listing.id}`;
  const revokeKey = `revoke:${listing.id}`;
  return (
    <div className="grid gap-4 border-b border-[#252a22] py-5 last:border-b-0 lg:grid-cols-[minmax(0,1fr)_150px_260px] lg:items-center">
      <div className="min-w-0">
        <div className="flex items-center gap-3">
          <h3 className="truncate font-medium">{listing.label}</h3>
          <span className={`text-xs uppercase tracking-[0.12em] ${listing.state === "active" ? "text-[#9ac36d]" : "text-[#c8877c]"}`}>{listing.state}</span>
        </div>
        <p className="mt-1 truncate font-mono text-xs text-[#65705f]">{listing.id}</p>
      </div>
      <p className="text-xs text-[#87947c]">{listing.last_seen ? `Seen ${new Date(listing.last_seen).toLocaleString()}` : "Not online yet"}</p>
      <div className="flex flex-wrap justify-start gap-2 lg:justify-end">
        {confirming === rotateKey ? (
          <ConfirmAction label="Rotate now" busy={busy} onConfirm={() => onRotate(listing.id)} onCancel={() => onConfirm(null)} />
        ) : confirming === revokeKey ? (
          <ConfirmAction label="Revoke now" busy={busy} danger onConfirm={() => onRevoke(listing.id)} onCancel={() => onConfirm(null)} />
        ) : (
          <>
            <Button size="sm" variant="ghost" disabled={busy || listing.state === "banned"} onClick={() => onConfirm(rotateKey)} className="text-[#b7c9a5] hover:bg-[#1b1d18] hover:text-white"><RefreshCw size={14} /> Rotate</Button>
            <Button size="sm" variant="ghost" disabled={busy || listing.state !== "active"} onClick={() => onConfirm(revokeKey)} className="text-[#c8877c] hover:bg-[#281a18] hover:text-[#ffb2a7]"><Trash2 size={14} /> Revoke</Button>
          </>
        )}
      </div>
    </div>
  );
};

const ConfirmAction = ({ label, busy, danger = false, onConfirm, onCancel }: { label: string; busy: boolean; danger?: boolean; onConfirm: () => void; onCancel: () => void }) => (
  <>
    <Button size="sm" onClick={onConfirm} disabled={busy} className={danger ? "bg-[#a84e43] text-white hover:bg-[#bf5d51]" : "bg-[#9ac36d] text-[#11140f] hover:bg-[#afd681]"}>{label}</Button>
    <Button size="sm" variant="ghost" onClick={onCancel} disabled={busy} className="text-[#87947c] hover:bg-[#1b1d18] hover:text-white">Cancel</Button>
  </>
);

const Notice = ({ children, tone = "neutral" }: { children: React.ReactNode; tone?: "neutral" | "error" }) => (
  <p className={`mt-6 border-l-2 px-4 py-3 text-sm ${tone === "error" ? "border-[#c45f52] bg-[#241715] text-[#ffb2a7]" : "border-[#65705f] bg-[#141611] text-[#b7c9a5]"}`}>{children}</p>
);

const MutationError = ({ error }: { error: unknown }) => (
  <p className="mt-3 text-sm text-[#ff9f9f]">{error instanceof Error ? error.message : "The portal action failed."}</p>
);

export default ServerPortalPage;
