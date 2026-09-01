export const SERVER_LIST_API = (import.meta.env.VITE_SERVER_LIST_API_URL ?? "https://list.s1servers.com").replace(/\/$/, "");

export type Provider = "discord" | "steam";

export type PortalIdentity = {
  provider: Provider;
  subject: string;
  display_name: string;
};

export type PortalAccount = {
  success: true;
  operatorId: string;
  csrfToken: string;
  identities: PortalIdentity[];
};

export type PortalListing = {
  id: string;
  label: string;
  state: "active" | "revoked" | "banned";
  created_at: number;
  updated_at: number;
  last_seen: number | null;
};

export type IssuedCredential = {
  listingId: string;
  secret: string;
  label?: string;
};

type PortalError = {
  success: false;
  error: string;
  message: string;
};

export class PortalApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code?: string,
  ) {
    super(message);
  }
}

export function authUrl(provider: Provider): string {
  return `${SERVER_LIST_API}/api/v2/portal/auth/${provider}/start`;
}

export async function getProviders(): Promise<Record<Provider, boolean>> {
  const response = await portalFetch<{ success: true; providers: Record<Provider, boolean> }>("/api/v2/portal/providers");
  return response.providers;
}

export function getAccount(): Promise<PortalAccount> {
  return portalFetch<PortalAccount>("/api/v2/portal/me");
}

export async function getListings(): Promise<PortalListing[]> {
  const response = await portalFetch<{ success: true; listings: PortalListing[] }>("/api/v2/portal/listings");
  return response.listings;
}

export function createListing(label: string, csrfToken: string): Promise<{ success: true } & IssuedCredential> {
  return portalFetch("/api/v2/portal/listings", {
    method: "POST",
    headers: mutationHeaders(csrfToken),
    body: JSON.stringify({ label }),
  });
}

export function rotateListing(listingId: string, csrfToken: string): Promise<{ success: true } & IssuedCredential> {
  return portalFetch(`/api/v2/portal/listings/${encodeURIComponent(listingId)}/rotate`, {
    method: "POST",
    headers: mutationHeaders(csrfToken),
  });
}

export function revokeListing(listingId: string, csrfToken: string): Promise<{ success: true }> {
  return portalFetch(`/api/v2/portal/listings/${encodeURIComponent(listingId)}`, {
    method: "DELETE",
    headers: mutationHeaders(csrfToken),
  });
}

export function logout(csrfToken: string): Promise<{ success: true }> {
  return portalFetch("/api/v2/portal/logout", {
    method: "POST",
    headers: mutationHeaders(csrfToken),
  });
}

export function buildPublicListingToml(credential: IssuedCredential): string {
  return `[publicListing]\npublicListingEnabled = true\npublicListingId = "${credential.listingId}"\npublicListingSecret = "${credential.secret}"`;
}

async function portalFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${SERVER_LIST_API}${path}`, {
    ...init,
    credentials: "include",
    headers: { Accept: "application/json", ...init?.headers },
  });
  const body = (await response.json().catch(() => null)) as T | PortalError | null;
  if (!response.ok) {
    const error = body as PortalError | null;
    throw new PortalApiError(error?.message ?? "The server portal request failed.", response.status, error?.error);
  }
  if (!body) {
    throw new PortalApiError("The server portal returned an empty response.", response.status);
  }
  return body as T;
}

function mutationHeaders(csrfToken: string): HeadersInit {
  return { "Content-Type": "application/json", "X-CSRF-Token": csrfToken };
}
